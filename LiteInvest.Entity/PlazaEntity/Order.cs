using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

namespace LiteInvest.Entity.PlazaEntity
{

    /// <summary>
    /// order
    /// ордер
    /// </summary>
    [DataContract]
    public class Order : IOrder
    {
        /// <summary>
        /// Создание ордера типа Маркет, рыночного, без указания цены
        /// 
        /// </summary>
        /// <param name="security"></param>
        /// <param name="side"></param>
        /// <param name="volume"></param>
        /// <param name="portfolio"></param>
        /// <param name="comment"></param>
        public Order(Security security, Side side, decimal volume, string portfolio, string comment)
        {
            decimal price = side == Side.Buy ? security.PriceLimitHigh : security.PriceLimitLow;
            SetPropertyNewOrder(security.Id, side, volume, price, portfolio, comment, OrderType.Market);
        }

        /// <summary>
        /// Создание ордера типа Маркет, рыночного, без указания цены, инструмент по Id
        /// </summary>
        /// <param name="securityId"></param>
        /// <param name="side"></param>
        /// <param name="volume"></param>
        /// <param name="portfolio"></param>
        /// <param name="comment"></param>
        public Order(string securityId, Side side, decimal volume, string portfolio, string comment)
        {
            SetPropertyNewOrder(securityId, side, volume, 0, portfolio, comment, OrderType.Market);
        }

        /// <summary>
        /// Создание Ордера типа Limit, с указанием цены
        /// </summary>
        /// <param name="security"></param>
        /// <param name="side"></param>
        /// <param name="volume"></param>
        /// <param name="price"></param>
        /// <param name="portfolio"></param>
        /// <param name="comment"></param>
        public Order(Security security, Side side, decimal volume, decimal price, string portfolio, string comment)
        {
            SetPropertyNewOrder(security.Id, side, volume, price, portfolio, comment, OrderType.Limit);
        }

        /// <summary>
        /// Создание Ордера типа Limit, с указанием цены, инструмент  по Id
        /// </summary>
        /// <param name="securityId"></param>
        /// <param name="side"></param>
        /// <param name="volume"></param>
        /// <param name="price"></param>
        /// <param name="portfolio"></param>
        /// <param name="comment"></param>
        public Order(string securityId, Side side, decimal volume, decimal price, string portfolio, string comment)
        {
            SetPropertyNewOrder(securityId, side, volume, price, portfolio, comment, OrderType.Limit);
        }

        private void SetPropertyNewOrder(string securityId, Side side, decimal volume, decimal price, string portfolio, string comment, OrderType orderType)
        {
            this.SecurityId = securityId;
            this.side = side;
            this.volume = volume;
            PortfolioNumber = portfolio;
            this.Comment = comment;
            this.priceOrder = price;
            typeOrder = orderType;

            state = OrderStateType.Pending;

            ExchangeOrderId = "";
            numberUser = PLazaHelper.CreateHashId();
            _trades = new List<MyTrade>();
            timeCreate = PLazaHelper.GetTimeMoscowNow();
        }

        public Order()
        {
            /*state = OrderStateType.None;
            timeCreate = DateTime.MinValue;
            timeCallBack = DateTime.MinValue;
            timeCancel = DateTime.MinValue;
            timeDone = DateTime.MinValue;
            numberMarket = "";
            this.side = Side.None;
            */
        }

        [DataMember]
        public string SecurityId { get; set; }

		[DataMember]
		public string SecIsin { get; set; }

		/// <summary>
		/// внутреннее для Артема
		/// Пытаемя уже отменить ордер
		/// </summary>
		private bool cancellingOrder { get; set; }

        /// <summary>
        /// дописано Артемом - 
        /// устанавливаем из вне 
        /// </summary>
        private decimal FilledVolume { get; set; }

        /// <summary>
        /// присвоен ли бирже номер
        /// </summary>
        private bool IsReal => ExchangeOrderId == "" ? false : true;


        [DataMember]
        internal int numberUser;


        /// <summary>
        /// Пользовательский Ордер Id
        /// </summary>
        [DataMember]
        public int NumberUserOrderId { get => numberUser; set { numberUser = value; } }


        /// <summary>
        /// order number on the exchange
        /// номер ордера на бирже
        /// </summary>
        [DataMember]
        public string ExchangeOrderId { get; set; }

        public void SetNumberMarket(string numberMarket)
        {
            this.ExchangeOrderId = numberMarket;   
            OrdersNumberHash[numberMarket] = numberUser;
        }

        /// <summary>
        /// Статический метод ConcurrentDictionary хранения номеров key = NumberMarket, Value=numberUser;
        /// </summary>
        internal static ConcurrentDictionary<string, int> OrdersNumberHash = new();


        /// <summary>
        /// account number to which the order belongs
        /// номер счёта которому принадлежит ордер
        /// </summary>
        [DataMember]
        public string PortfolioNumber { get; set; }


        /// <summary>
        /// direction
        /// направление
        /// </summary>
        [DataMember]
        internal Side side;

        [DataMember]
        public Side Side { get => side; set { side = value; } }


        /// <summary>
        /// Цена Ордера
        /// </summary>
        [DataMember]
        public decimal PriceOrder { get => priceOrder; set { priceOrder = value; } }
        internal decimal priceOrder;


        /// <summary>
        /// real price
        /// Средняя цена исполнения
        /// </summary>
        public decimal PriceReal
        {
            get { return GetMidlePrice(); }
        }


        /// <summary>
        /// volume
        /// объём
        /// </summary>
        [DataMember]
        public decimal Volume { get => volume; set { volume = value; } }
        internal decimal volume;

        [DataMember]
        public string Error { get; set; }


        /*
        /// <summary>
        /// execute volume
        /// объём исполнившийся
        /// </summary>
        public decimal VolumeExecute
        {
            get
            {
                if (_trades != null && (volumeExecute == 0 || _volumeExecuteChange))
                {
                    volumeExecute = 0;

                    for (int i = 0; i < _trades.Count; i++)
                    {
                        if (_trades[i] == null)
                        {
                            continue;
                        }

                        volumeExecute += _trades[i].Volume;
                    }

                    _volumeExecuteChange = false;
                    return volumeExecute;
                }
                else
                {
                    if (volumeExecute == 0 && State == OrderStateType.Done)
                    {
                        return Volume;
                    }

                    return volumeExecute;
                }

            }

        }*/

        /// <summary>
        /// Исполненный объем
        /// </summary>
        [DataMember]
        public decimal VolumeExecuted { get; set; }

        private bool _volumeExecuteChange;

        /// <summary>
        /// Сделки по заявке
        /// </summary>
        private List<MyTrade> MyTrades
        {
            get { return _trades; }
        }

        /// <summary>
        /// order status: None, Pending, Done, Patrial, Fail
        /// статус ордера: None, Pending, Done, Patrial, Fail
        /// </summary>
        [DataMember]
        public OrderStateType State
        {
            get { return state; }
            set {  state = value; }
        }

        internal OrderStateType state;

        /// <summary>
        /// order price type. Limit, Market
        /// тип цены ордера. Limit, Market
        /// </summary>
        [DataMember]
        public OrderType TypeOrder { get => typeOrder; set { typeOrder = value; } }
        private OrderType typeOrder;

        /// <summary>
        /// why the order was created in the context of the position. Open is the opening order. Close is the closing order
        /// для чего создан ордер в контексте позиции. Open - для открытия позиции. Close - для закрытия позиции
        /// </summary>
        private OrderPositionConditionType PositionConditionType;

        /// <summary>
        /// user comment
        /// комментарий пользователя
        /// </summary>
        [DataMember]
        public string Comment { get; set; }

        /// <summary>
        /// time of the first response from the stock exchange on the order. Server time
        /// время первого отклика от биржи по ордеру. Время севрера.
        /// </summary>
        [DataMember]
        public DateTime TimeCallBack { get => timeCallBack; set { timeCallBack = value; } }
        internal DateTime timeCallBack;

        /// <summary>
        /// time of order removal from the system. Server time
        /// время снятия ордера из системы. Время сервера
        /// </summary>
        [DataMember]
        public DateTime TimeCancel { get => timeCancel; set { timeCancel = value; } }
        internal DateTime timeCancel;

        /// <summary>
        /// order execution time. Server time
        /// время исполнения ордера. Время сервера
        /// </summary>
        [DataMember]
        public DateTime TimeDone { get => timeDone; set { timeDone = value; } }
        internal DateTime timeDone;

        /// <summary>
        /// order creation time.
        /// время создания ордера.
        /// </summary>
        [DataMember]
        public DateTime TimeCreate { get => _timeCreate; set { _timeCreate = value; } }

        /// <summary>
        /// Время создания ордера.
        /// </summary>
        internal DateTime timeCreate
        {
            get
            {
                return _timeCreate;
            }
            set
            {
                if (_timeCreate == DateTime.MinValue)
                {
                    _timeCreate = value;
                }
            }
        }

        private DateTime _timeCreate;
        public DateTime TimeSet
        {
            get
            {
                return _timeSet;
            }
            set
            {
                if (_timeSet == DateTime.MinValue)
                {
                    _timeSet = value;
                }
            }
        }
        private DateTime _timeSet;


        /// <summary>
        /// bidding rate
        /// скорость выставления заявки
        /// </summary>
        public TimeSpan TimeRoundTrip
        {
            get
            {
                if (TimeSet == DateTime.MinValue ||
                    TimeCreate == DateTime.MinValue 
                    || TimeSet < TimeCreate)
                {
                    return new TimeSpan(0, 0, 0, 0);
                }

                return (TimeSet - TimeCreate);
            }
        }

        /// <summary>
        /// /// time when the order was the first transaction
        /// if there are no deals on the order yet, it will return the time to create the order
        /// время когда по ордеру прошла первая сделка
        /// если сделок по ордеру ещё нет, вернёт время создания ордера
        /// </summary>
        private DateTime TimeExecuteFirstTrade
        {
            get
            {
                if (MyTrades == null ||
                    MyTrades.Count == 0)
                {
                    return TimeCreate;
                }

                return MyTrades[0].Time;
            }
        }

        /// <summary>
        /// lifetime on the exchange, after which the order must be withdrawn
        /// время жизни на бирже, после чего ордер надо отзывать
        /// </summary>
        private TimeSpan LifeTime;

        /// <summary>
        /// /// flag saying that this order was created to close by stop or profit order
        /// the tester needs to perform it adequately
        /// флаг,говорящий о том что этот ордер был создан для закрытия по стоп или профит приказу
        /// нужен тестеру для адекватного его исполнения
        /// </summary>
        private bool IsStopOrProfit;

        //public ServerType ServerType;
        // deals with which the order was opened and calculation of the order execution price
        // сделки, которыми открывался ордер и рассчёт цены исполнения ордера

        /// <summary>
        /// order trades
        /// сделки ордера
        /// </summary>
        private List<MyTrade> _trades;


        /*
        /// <summary>
        /// heck the ownership of the transaction to this order
        /// проверить принадлежность сделки этому ордеру
        /// </summary>
        private void SetTrade(MyTrade trade)
        {
            if (_trades != null &&
                _trades.Count > 0
                && State == OrderStateType.Done
                && Volume == VolumeExecute)
            {
                return;
            }

            if (trade.NumberOrderMarket != ExchangeOrderId)
            {
                return;
            }

            if (_trades != null)
            {
                foreach (var tradeInArray in _trades)
                {
                    if (tradeInArray.NumberTrade == trade.NumberTrade)
                    {
                        // / such an application is already in storage, a stupid API is poisoning with toxic data, we exit
                        // такая заявка уже в хранилище, глупое АПИ травит токсичными данными, выходим
                        return;
                    }
                }
            }

            _volumeExecuteChange = true;

            if (_trades == null)
            {
                _trades = new List<MyTrade>();
            }

            _trades.Add(trade);

            if (Volume != VolumeExecute)
            {
                state = OrderStateType.Partial;
            }
            else
            {
                state = OrderStateType.Done;
            }
        }*/

        /// <summary>
        /// take the average order execution price
        /// взять среднюю цену исполнения ордера
        /// </summary>
        private decimal GetMidlePrice()
        {
            decimal price = 0;

            if (_trades == null)
            {
                return price;
            }

            decimal volumeExecute = 0;

            for (int i = 0; i < _trades.Count; i++)
            {
                price += _trades[i].Volume * _trades[i].Price;
                volumeExecute += _trades[i].Volume;
            }

            if (volumeExecute == 0)
            {
                return price;
            }

            price = price / volumeExecute;

            return price;
        }

        /// <summary>
        /// take the time of execution of the last trade on the order
        /// взять время исполнения последнего трейда по ордеру
        /// </summary>
        private DateTime TimeExecuteLastTrade()
        {
            if (_trades == null)
            {
                return TimeCallBack;
            }

            return _trades[_trades.Count - 1].Time;
        }

        /// <summary>
        /// whether the trades of this order came to the array
        /// пришли ли трейды этого ордера в массив
        /// </summary>
        private bool TradesIsComing
        {
            get
            {
                if (_trades == null
                    || _trades.Count == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }

            }
        }



        private static readonly CultureInfo CultureInfo = new CultureInfo("ru-RU");

        /// <summary>
        /// ToString()
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return new StringBuilder()
                .Append(numberUser).Append("; ")
                .Append(ExchangeOrderId).Append("; ")
                .Append(SecurityId).Append("; ")
                .Append(side == Side.Buy? "Buy" : side == Side.Sell ? "Sell" : "None").Append("; ")
                .Append(priceOrder).Append("; ")
                .Append(volume).Append("; ")
                .Append(TypeOrder).Append("; ")
                .Append(Comment).Append("; ")
                .Append(PortfolioNumber).Append("; ")
                .Append(state).Append("; ")
                .ToString();
        }

        /// <summary>
        /// price type for order
        /// тип цены для ордера
        /// </summary>
        [DataContract]
        public enum OrderType
        {
            /// <summary>
            /// limit order. Those. bid at a certain price
            /// лимитная заявка. Т.е. заявка по определённой цене
            /// </summary>
            [EnumMember]
            Limit,

            /// <summary>
            /// market application. Those. application at any price
            /// рыночная заявка. Т.е. заявка по любой цене
            /// </summary>
            [EnumMember]
            Market,

            /// <summary>
            /// iceberg application. Those. An application whose volume is not fully visible in the glass.
            /// айсберг заявка. Т.е. заявка объём которой полностью не виден в стакане. 
            /// Не реализовано
            /// </summary>
            //Iceberg,


        }

        /// <summary>
        /// Order status
        /// статус Ордера
        /// </summary>
        [DataContract]
        public enum OrderStateType
        {
            /// <summary>
            /// none
            /// отсутствует
            /// </summary>
            [EnumMember]
            None,

            /// <summary>
            /// accepted by the exchange and exhibited in the system
            /// принята биржей и выставленна в систему
            /// </summary>
            [EnumMember]
            Activ,

            /// <summary>
            /// waiting for registration
            /// ожидает регистрации
            /// </summary>
            [EnumMember]
            Pending,

            /// <summary>
            /// done
            /// исполнен
            /// </summary>
            [EnumMember]
            Done,

            /// <summary>
            /// partitial done
            /// исполнен частично
            /// </summary>
            [EnumMember]
            Partial,

            /// <summary>
            /// error
            /// произошла ошибка
            /// </summary>
            [EnumMember]
            Fail,

            /// <summary>
            /// cancel
            /// отменён
            /// </summary>
            [EnumMember]
            Cancel
        }

        public enum OrderPositionConditionType
        {
            None,
            Open,
            Close
        }
    }
}
