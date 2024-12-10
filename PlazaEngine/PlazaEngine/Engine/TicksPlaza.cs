using ru.micexrts.cgate;
using ru.micexrts.cgate.message;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteInvest.Entity.PlazaEntity;

namespace PlazaEngine.Engine
{

    /// <summary>
    /// Класс отвечающий за подписку на тики - обезличенные сделки
    /// </summary>
    public class TicksPlaza :IDisposable
    {

        private PlazaConnector plazaConnector;
        private Listener? listener;

        private Thread threadNotification;
        private CancellationTokenSource cancelTokenSource;
        private CancellationToken token;
        private int periodMilliSecond;

        /// <summary>
        /// Пришел новый тик - обезличенная сделка
        /// key = SecurityID, value = List Trade
        /// </summary>
        internal event Action<Dictionary<string, List<Trade>>>? NewTickCollectionEvent;

        internal event Action<Trade>? NewTickEvent;

        /// <summary>
        /// Все тики загружены, подписка вышла в режим онлайн
        /// </summary>
        internal event Action AllTicksLoadedEvent;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerTradeNeadToReconnect;

        /// <summary>
		/// thread state of tick listener
        /// состояние потока листнера тиков
        /// </summary>
        private string _listenerTradeReplState;

        /// <summary>
        /// initialization string of the Listner responsible for receiving ticks
        /// строка инициализации листнера отвечающего за приём тиков
        /// </summary>
        private const string ListenTradeString = "p2repl://FORTS_DEALS_REPL;scheme=|FILE|SchemasPlaza/Schemas/deals.ini|CustReplScheme";

        /// <summary>
        /// Класс отвечающий за подписку на тики - обезличенные сделки
        /// </summary>
        /// <param name="plazaConnector"></param>
        /// <param name="periodMilliSecond"></param>
        public TicksPlaza(PlazaConnector plazaConnector, int periodMilliSecond)
        {
            this.periodMilliSecond = periodMilliSecond;
            this.plazaConnector = plazaConnector;

           


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

            listener = _listener;
            listener.Handler += new Listener.MessageHandler(TicksMessageHandler);

            cancelTokenSource?.Cancel();
            Thread.Sleep(1000);
            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;
            

            threadNotification = new Thread(ThreadNotificationTicks);
            threadNotification.Name = "ThreadNotificationTicks";
            threadNotification.IsBackground = true;
            threadNotification.Start();
        }

        bool _dealsOnLine = false;

        /// <summary>
        /// tick listener
        /// листнер тиков
        /// </summary>
        private Listener _listenerTrade;

        /// <summary>
        /// key = SecurityId, value = список трейдов
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentQueue<Trade>> Ticks = new ConcurrentDictionary<string, ConcurrentQueue<Trade>>();

        /// <summary>
        /// Это мой экспериментальный вариант для выдергивания всех тиков... 
        /// </summary>
        public ConcurrentDictionary<string, Trade> AllTicks = new();

        private int TicksMessageHandler(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "deal")
                            {
                                // ticks/тики
                                try
                                {
                                    byte isSystem = replmsg["nosystem"].asByte();

                                    if (isSystem == 1)
                                    {
                                        return 0;
                                    }
                                    var isin_id = replmsg["isin_id"].asInt().ToString();

                                    Trade trade = new Trade(plazaConnector.Securities);

                                    trade.TransactionID = replmsg["id_deal"].asLong().ToString();
                                    trade.SecurityId = replmsg["isin_id"].asInt().ToString();
                                    if (plazaConnector.Securities.TryGetValue(replmsg["isin_id"].asInt().ToString(), out Security? _secname))
                                        trade.SecurityName = _secname.Name;
                                    trade.Time = replmsg["moment"].asDateTime();
                                    trade.IsOnline = _dealsOnLine;
                                    trade.Side = replmsg["public_order_id_buy"].asLong() > replmsg["public_order_id_sell"].asLong() ? Side.Buy : Side.Sell;
                                    trade.Volume = replmsg["xamount"].asInt();
                                    trade.Price = Convert.ToDecimal(replmsg["price"].asDecimal());

                                    AllTicks[trade.SecurityId] = trade;
                                    
                                    if (plazaConnector.RegisteredTicks.Contains(isin_id))
                                    {

                                        
                                        /*
                                        Trade trade = new Trade(replmsg, _dealsOnLine, plazaConnector.Securities);*/

                                        if (!Ticks.ContainsKey(trade.SecurityId))
                                        {
                                            Ticks[trade.SecurityId] = new();
                                        }

                                        Ticks[trade.SecurityId].Enqueue(trade);
                                        
                                        NewTickEvent?.Invoke(trade);

                                        //RouterLogger.Log(trade.ToString(), "Ticks");
                                    }
                                }
                                catch (Exception error)
                                {
                                    plazaConnector.SendLogMessage(error);
                                }
                            }

                            break;
                        }

                    case MessageType.MsgP2ReplOnline:
                        {
                            RouterLogger.Log("Тики полностью загрузились", "Ticks");
                            _dealsOnLine = true;
                            AllTicksLoadedEvent?.Invoke();
                            
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerTradeReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerTradeNeadToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException e)
            {
                return (int)e.ErrCode;
            }

            return 0;
        }

        private void ThreadNotificationTicks()
        {
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(periodMilliSecond);
                try
                {
                    var ticksCollectionForOut = new Dictionary<string, List<Trade>>();
                    var keys = Ticks?.Keys.ToList();
                    bool needEvent = false;
                    if (keys is null)
                    {
                        continue;
                    }
                    for (int i = 0; i < (keys?.Count??0); i++)
                    {
                        ticksCollectionForOut[keys[i]] = new List<Trade>();
                        var q = Ticks[keys[i]];
                        while (q.TryDequeue(out Trade t))
                        {
                            ticksCollectionForOut[keys[i]].Add(t);
                            needEvent = true;
                        }
                    }
                    if (needEvent)
                    {
                        NewTickCollectionEvent?.Invoke(ticksCollectionForOut);
                    }
                }
                catch (Exception ex)
                {
                    plazaConnector.SendLogMessage(ex);
                }

            }
        }

        internal void CheckListnerTrades()
        {
            try
            {
                if (_listenerTrade == null)
                {
                    _listenerTrade = new Listener(plazaConnector.ConnectionObjectPlaza, ListenTradeString);
                    SetListener(_listenerTrade);

                }
                if (_listenerTradeNeadToReconnect || _listenerTrade.State == State.Error)
                {
                    _listenerTradeNeadToReconnect = false;
                    try
                    {
                        _listenerTrade.Close();
                        _listenerTrade.Dispose();
                    }
                    catch (Exception error)
                    {
                        plazaConnector.SendLogMessage(error);
                    }

                    _listenerTrade = null;
                    return;
                }
                if (_listenerTrade.State == State.Closed)
                {
                    if (_listenerTradeReplState == null)
                    {
                        if (! plazaConnector.LoadTicksFromStart)
                            _listenerTrade.Open("mode=online");
                        else
                            _listenerTrade.Open("mode=snapshot+online");
                        RouterLogger.Log("Загрузка тиков пошла", "Connect");
                    }
                    else
                    {
                        if (! plazaConnector.LoadTicksFromStart)
                            _listenerTrade.Open("mode=online;" + "replstate=" + _listenerTradeReplState);
                        else
                            _listenerTrade.Open("mode=snapshot+online;" + "replstate=" + _listenerTradeReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerTrade.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerTrade = null;

                plazaConnector.SendLogMessage(error);
            }
        }

        public void Dispose()
        {
            try { listener?.Close(); } catch { }
            try { listener?.Dispose(); } catch { }
            cancelTokenSource?.Cancel();
            
            //NewTickCollectionEvent = null;
            //AllTicksLoadedEvent = null;

        }
    }
}