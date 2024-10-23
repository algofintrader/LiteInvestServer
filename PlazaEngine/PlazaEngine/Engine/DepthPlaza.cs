
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

using ru.micexrts.cgate;
using System.Runtime.InteropServices;
using ru.micexrts.cgate.message;
using System.Reflection.Metadata;
using PlazaEngine.Engine;
using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Transactions;
using System.Reflection;

namespace PlazaEngine.Depth
{
    internal class DepthPlaza : IDisposable
    {
        PlazaConnector plazaConnector;
        Listener? listener;
        
        ConcurrentDictionary<UInt32, OrderBook> orderBooks;
        Int64 lastRevision;
        HashSet<UInt32> subscriptedIsin = [];
        
        Thread threadNotification;
        CancellationTokenSource cancelTokenSource;
        CancellationToken token;

        int UpDateTimeMs { get; set; }

        public DepthPlaza(PlazaConnector _plazaConnector,int _updateTimeMs = 20)
        {
            plazaConnector = _plazaConnector;
            orderBooks = new();
            schemeInfo.Ready = false;
            schemeInfo.Indices.OrdersAggr = -1;
            lastRevision = 0;

            UpDateTimeMs = _updateTimeMs;

            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;

            threadNotification = new Thread(ThreadNotificationDepth);
            threadNotification.Name = "ThreadNotificationDepth";
            threadNotification.IsBackground = true;
            threadNotification.Start();

        }

        public void SetListener(Listener _listener)
        {
            if (listener != null)
            {
                try
                {
                    listener.Handler = null;
                    listener.Close();
                    listener = null;
                }
                catch 
                {
                    listener = null;
                }
            }
            MarketDepthChangeEvent = null;
            listener = _listener;
            listener.Handler += new Listener.MessageHandler(OrdbookMessageHandler);
        }

        bool newSubscription = false;

        public void Subscription(string isinId)
        {
            if (UInt32.TryParse(isinId, out uint isin))
            {
                subscriptedIsin.Add(isin);
            }
            else
            {
                throw new Exception($"Invalid isin_id specified for subscription. {isinId}");
            }
            newSubscription = true;
        }

        public void UnSubscription(string isinId)
        {
            if (UInt32.TryParse(isinId, out uint isin))
            {
                subscriptedIsin.Remove(isin);
            }
            else
            {
                throw new Exception($"Invalid isin_id specified for unsubscription. {isinId}");
            }
        }

        public event Action<MarketDepth>? MarketDepthChangeEvent;
        public event Action MarketDepthLoadedEvent;

        public struct SchemeInfo
        {

            public struct TableIndices
            {
                public long OrdersAggr;
            };
            public TableIndices Indices;


            public struct TableNames
            {
                public const string OrdersAggr = "orders_aggr";
            };


            public struct FieldNames
            {
                public const string ReplId = "replID";
                public const string ReplRevision = "replRev";
                public const string ReplAct = "replAct";
                public const string IsinId = "isin_id";
                public const string Price = "price";
                public const string Amount = "volume";
                public const string Moment = "moment";
                public const string Moment_ns = "moment_ns";
                public const string Direction = "dir";
                public const string SessionId = "sess_id";
            };

            public bool Ready;

            public const Int32 StatusMessageNonSystem = 0x4;
            public const Int32 StatusEndBusinessTransaction = 0x1000;

        };
        SchemeInfo schemeInfo;
        

        ~DepthPlaza()
        {
            try
            {
                this.listener?.Close();
            }
            catch (CGateException ex)
            {
                CGate.LogError(ex.Message);
            }
            catch (Exception) { }
            
            this.Cleanup();
        }

      

        int OrdbookMessageHandler(Connection connection, Listener listener, Message message)
        {
            try
            {
                switch (message.Type)
                {

                    case MessageType.MsgOpen:
                        {
                           
                            this.Cleanup();

                            for (int i = 0; i < listener.Scheme.Messages.Length; ++i)
                            {
                                MessageDesc md = listener.Scheme.Messages[i];
                                if (md.Name == SchemeInfo.TableNames.OrdersAggr)
                                {
                                    schemeInfo.Indices.OrdersAggr = i;
                                }
                            }

                            if (schemeInfo.Indices.OrdersAggr >= 0)
                            {
                                schemeInfo.Ready = true;
                            }
                            break;
                        }

                    case MessageType.MsgStreamData:
                        {
                            if (!schemeInfo.Ready)
                            {
                                break;
                            }
                            StreamDataMessage sdMessage = (StreamDataMessage)message;

                            if (sdMessage.MsgIndex == schemeInfo.Indices.OrdersAggr)
                            {
                                UInt32 isinId = sdMessage[SchemeInfo.FieldNames.IsinId].asUInt();
                                OrderAggr order = new OrderAggr(sdMessage);
                                
                                if (!orderBooks.ContainsKey(isinId))
                                {
                                    orderBooks[isinId] = new OrderBook(isinId);

                                }
                                orderBooks[isinId].Add(order);
                                ulong moment = sdMessage[SchemeInfo.FieldNames.Moment_ns].asULong();
                                orderBooks[isinId].SetMoment(moment);
                                lastRevision = order.Revision.TableRevision;
                            }
                            break;
                        }

                    case MessageType.MsgP2ReplOnline:
                        {
                            MarketDepthLoadedEvent?.Invoke();
                            Print($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\RouterPlaza\\log\\OnlineDepth.log");
                            break;
                        }

                    case MessageType.MsgP2ReplLifeNum:
                        {
                            Cleanup();
                            break;
                        }

                    case MessageType.MsgP2ReplClearDeleted:
                        {
                            P2ReplClearDeletedMessage cdMessage = (P2ReplClearDeletedMessage)message;
                            
                            if (cdMessage.TableIdx == this.schemeInfo.Indices.OrdersAggr)
                            {
                                RevisionInfo revision = new RevisionInfo(cdMessage.TableIdx, cdMessage.TableRev);

                                foreach (var item in orderBooks)
                                {
                                    item.Value.OnClearDeleted(revision);
                                }

                                if (cdMessage.TableRev == P2ReplClearDeletedMessage.MaxRevision)
                                {
                                    lastRevision = 0;
                                }
                            }
                            break;
                        }

                    case MessageType.MsgClose:
                        {
                            Cleanup();
                            break;
                        }
                }

                if (DateTime.Now.Subtract(PrintLastRevisionTime).TotalSeconds > 10)
                {
                    //PrintLastRevision();
                    PrintLastRevisionTime = DateTime.Now;
                }

                return (int)ErrorCode.Ok;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return 0;
            }
        }

        DateTime PrintLastRevisionTime = DateTime.Now;

        void Cleanup()
        {
            this.orderBooks.Clear();
        }

        void Print(string fileName)
        {
            TextWriter writer = new StreamWriter(fileName);
            writer.WriteLine(orderBooks.Count);
            var keys = orderBooks.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var orderBook = orderBooks[key];
                writer.WriteLine($"{key}");
                writer.WriteLine($"BUY: {orderBook.GetMoment():yyyy.MM.dd HH:mm:ss.fff}");
                orderBook.Print(Direction.Buy, writer);
                writer.WriteLine("SELL:");
                orderBook.Print(Direction.Sell, writer);
            }
            writer.Close();
        }


        void PrintLastRevision()
        {
            this.Print($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\RouterPlaza\\log\\AggregateDepth" + "." + this.lastRevision + ".log");
        }

        private void ThreadNotificationDepth()
        {
            Dictionary<uint,MarketDepth> mdList = new Dictionary<uint,MarketDepth>();
            Int64 lastRevisionSended = 0;
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(UpDateTimeMs);
                try
                {
                    if ((subscriptedIsin?.Count ?? 0) == 0)
                    {
                        continue;
                    }
                    if (lastRevisionSended == lastRevision && !newSubscription)
                    {
                        continue;
                    }
                    lastRevisionSended = lastRevision;

                    List<uint> _allIsin = subscriptedIsin.ToList();
                    for (int i = 0; i < _allIsin.Count; i++)
                    {
                        uint isin = _allIsin[i];
                        if (orderBooks.TryGetValue(isin, out var orderBook))
                        {
                            if (orderBook.IsNewRevision || newSubscription)
                            {
                                mdList.TryGetValue(isin, out MarketDepth? md);
                                md = orderBook.GetMarketDepth(md);
                                if (!mdList.ContainsKey(isin))
                                {
                                    mdList[isin] = md;
                                }
                                MarketDepthChangeEvent?.Invoke(md);
                                newSubscription = false;
                            }
                        }
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        public void Dispose()
        {
            try
            {
                listener?.Close();
                Cleanup();
                cancelTokenSource.Cancel();
                MarketDepthChangeEvent = null;
                MarketDepthLoadedEvent = null;
            }
            catch (Exception ex)
            {
                CGate.LogError(ex.Message);
            }


        }
    };

	class RevisionInfo
	{
		private UInt32 tableIndex;
		private Int64 tableRevision;
	
		public UInt32 TableIndex
		{
			get
			{
				return tableIndex;
			}
		}
	
		public Int64 TableRevision
		{
			get
			{
				return tableRevision;
			}
		}
	
		public RevisionInfo(UInt32 index, Int64 revision)
		{
			this.tableIndex = index;
			this.tableRevision = revision;
		}
	
		public RevisionInfo(P2ReplClearDeletedMessage message)
		{
			this.tableIndex = message.TableIdx;
			this.tableRevision = message.TableRev;
		}
	
		public override string ToString() 
		{
			return String.Format("table = {0}, rev = {1}",
				this.tableIndex, this.tableRevision);
		}
	};

	/*
	class MessageLogHelper
	{
		static string MessagePrefix(MessageType type)
		{
			return type.ToString() + ". ";
		}

		public static string CreateMessageString(Message message)
		{
			return MessagePrefix(message.Type);
		}

		public static string CreateMessageString(P2ReplClearDeletedMessage message)
		{
			return MessagePrefix(message.Type) + new RevisionInfo(message).ToString();
		}

		public static string CreateMessageString(StreamDataMessage message)
		{
			return CreateMessageString(new OrderAggr(message));
		}

		public static string CreateMessageString(OrderAggr order)
		{
			return MessagePrefix(MessageType.MsgStreamData) + order.ToString();
		}
	};
	*/
	
	class OrderAggr
	{
		double price;
		Int32 amount;
		RevisionInfo revision;
		Direction direction;
		Int32 isinId;
		Int64 replId;
		Int64 replAct;

		public OrderAggr(StreamDataMessage message)
		{
			price = message[DepthPlaza.SchemeInfo.FieldNames.Price].asDouble();
			amount = message[DepthPlaza.SchemeInfo.FieldNames.Amount].asInt();
			Int64 tableRevision = message[DepthPlaza.SchemeInfo.FieldNames.ReplRevision].asLong();
			revision = new RevisionInfo((uint)message.MsgIndex, tableRevision);
		
			Byte intDir = message[DepthPlaza.SchemeInfo.FieldNames.Direction].asByte();
			direction = (intDir == 1) ? Direction.Buy : Direction.Sell;
			isinId = message[DepthPlaza.SchemeInfo.FieldNames.IsinId].asInt();
			replAct = message[DepthPlaza.SchemeInfo.FieldNames.ReplAct].asLong();
			replId = message[DepthPlaza.SchemeInfo.FieldNames.ReplId].asLong();
		}
				
		public double Price
		{
			get 
			{
				return this.price;
			}
		}
	
		public Int32 Amount
		{
			get
			{
				return this.amount;
			}
			set
			{
				this.amount = value;
			}
		}
	
		public RevisionInfo Revision
		{
			get
			{
				return this.revision;
			}
		}
	
		public Direction Direction
		{
			get
			{
				return this.direction;
			}
		}
	
		public Int32 IsinId
		{
			get
			{
				return this.isinId;
			}
		}

		public Int64 ReplId
		{
			get
			{
				return this.replId;
			}
		}

		public Int64 ReplAct
		{
			get
			{
				return this.replAct;
			}
		}
	
		public override string ToString()
		{
			return String.Format(
				"Price = {0:F2}, amount = {1}, table = {2}, rev = {3}, Dir = {4}, IsinId = {5}, ReplAct = {6}",
				this.price, 
				this.amount, 
				this.revision.TableIndex, 
				this.revision.TableRevision,
				this.direction, 
				this.isinId, 
				this.replAct);
		}
	};
    	
	class Orders
	{
		Direction direction;
		Dictionary<Int64, OrderAggr> aggregateOrders;
	
		public Orders(Direction dir)
		{
			aggregateOrders = new Dictionary<Int64, OrderAggr>();
			direction = dir;
		}
	
		
		public void Add(OrderAggr order)
		{
			if (order.ReplAct != 0)
			{
				CGate.LogDebug(String.Format("Delete record {0}.", order.Price));
				aggregateOrders.Remove(order.ReplId);
			}
			try
			{
				if (aggregateOrders.TryGetValue(order.ReplId, out var existingOrder))
				{
					if (order.Amount == 0)
						aggregateOrders.Remove(order.ReplId);
					else
						aggregateOrders[order.ReplId] = order;
				}
				else
				{
                    aggregateOrders.Add(order.ReplId, order);
                }
			}
			catch(Exception ex)
			{
                CGate.LogDebug(String.Format("Error add record {0}.", ex));
            }
		}

		
		public void Clear()
		{
			aggregateOrders.Clear();
		}

		
		public void OnClearDeleted(RevisionInfo rev)
		{
			List<Int64> keysToRemove = new List<Int64>();
			foreach (KeyValuePair<Int64, OrderAggr> p in aggregateOrders)
			{
				if ((p.Value.Revision.TableIndex == rev.TableIndex) &&
					(p.Value.Revision.TableRevision < rev.TableRevision))
				{
					keysToRemove.Add(p.Key);
				}
			}
			foreach (Int64 key in keysToRemove)
			{
				aggregateOrders.Remove(key);
			}
		}

		
		public void Print(TextWriter writer)
		{
			SortedDictionary<double, Int32> sortedAggr = new SortedDictionary<double,int>();

			foreach (KeyValuePair<Int64, OrderAggr> item in aggregateOrders)
			{
				if (item.Value.Price > 0)
					sortedAggr.Add(item.Value.Price, item.Value.Amount);
			}
			foreach (KeyValuePair<double, Int32> item in sortedAggr)
			{
				writer.WriteLine("\t\tprice = {0:F2}\tamount = {1}", item.Key, item.Value);
			}
		}

        public SortedDictionary<decimal, Int32> GetSortedDepth(bool reverse)
        {
            SortedDictionary<decimal, Int32> sortedAggr = new ();
            foreach (KeyValuePair<Int64, OrderAggr> item in this.aggregateOrders)
            {
				if (item.Value.Price > 0)
				{
					sortedAggr.Add(Convert.ToDecimal(item.Value.Price), item.Value.Amount);
				}
            }
			return sortedAggr;
        }

    };

	
	class OrderBook
	{
		UInt32 isinId;
		Orders[] orders;
		DateTime moment = DateTime.MinValue;
        bool newRevision = false;

        object revisionLocker = new object();

        /// <summary>
        /// Unix Time in UTC nanosecund to DateTime MSK
        /// </summary>
        /// <param name="_moment"> Unix Time in nanosecund</param>
		public void SetMoment(ulong _moment)
		{
			var _m = DateTimeOffset.FromUnixTimeMilliseconds((long)(_moment / 1000000)).DateTime.AddHours(3); //utc to msk
            if (_m <= moment)
                moment = moment.AddTicks(1);
            else 
                moment = _m;
            lock (revisionLocker)
            {
                newRevision = true;
            }
        }

        public bool IsNewRevision
        {
            get
            {
                lock (revisionLocker)
                {
                    if (newRevision)
                    {
                        newRevision = false;
                        return true;
                    }
                    return false;
                }
            }

        }


        public DateTime GetMoment()
		{
			return moment;
		}
		

		public OrderBook(UInt32 isinId)
		{
			this.isinId = isinId;
			this.orders = new Orders[2];
			this.orders[(int)Direction.Buy] = new Orders(Direction.Buy);
			this.orders[(int)Direction.Sell] = new Orders(Direction.Sell);
		}
	
		public void Add(OrderAggr order)
		{
			this.orders[(int)order.Direction].Add(order);
		}
	
		public void Clear()
		{
			this.orders[(int)Direction.Buy].Clear();
			this.orders[(int)Direction.Sell].Clear();
		}
	
		public void OnClearDeleted(RevisionInfo rev)
		{
			this.orders[(int)Direction.Buy].OnClearDeleted(rev);
			this.orders[(int)Direction.Sell].OnClearDeleted(rev);
		}

		public void Print(Direction direction, TextWriter writer)
		{
			this.orders[(int)direction].Print(writer);
		}

		public MarketDepth GetMarketDepth(MarketDepth? md)
		{
			if (md is null) md = new();
            md.Asks?.Clear();
            md.Bids?.Clear();

			var mdBuy =  this.orders[(int)Direction.Buy].GetSortedDepth(false);
            var mdSell = this.orders[(int)Direction.Sell].GetSortedDepth(false);

            foreach (var item in mdBuy)
            {
                Entity.MarketDepthLevel MDLevel = new Entity.MarketDepthLevel();
				MDLevel.Price = item.Key;
				MDLevel.Bid = item.Value;
                //md.Bids.Add(MDLevel);
                md.Bids.Insert(0,MDLevel);
            }
            foreach (var item in mdSell)
            {
                Entity.MarketDepthLevel MDLevel = new Entity.MarketDepthLevel();
                MDLevel.Price = item.Key;
                MDLevel.Ask = item.Value;
                md.Asks.Insert(0,MDLevel);
                //md.Asks.Add(MDLevel);
            }
            mdBuy.Clear();
            mdSell.Clear();

            md.Time = GetMoment();
			md.SecurityId = isinId.ToString();
			return md;
        }
	};

    enum Direction
    {
        Buy, Sell
    };

}
