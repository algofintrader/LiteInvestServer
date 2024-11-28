using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Xml.Linq;
using PlazaEngine.Entity;
using ru.micexrts.cgate;
using ru.micexrts.cgate.message;
using PlazaEngine.Depth;
using RouterLoggerSpace;
using System.Runtime.CompilerServices;
using System.Data.SqlTypes;
using System.Net.NetworkInformation;
using System.Text;
using System.Runtime.Serialization;
using System.Timers;

//using SKM.V3;
//using SKM.V3.Models;

namespace PlazaEngine.Engine
{
    public enum ServerConnectStatus
    {
        /// <summary>
        /// connected
        /// подключен
        /// </summary>
        Connect,

        /// <summary>
        /// disconnected
        /// отключен
        /// </summary>
        Disconnect,
    }
    public class PlazaConnector : ConnectorBase
    {
        /// <summary>
        /// Тариф по числу транзакций в секунду (минимальный 30/сек)
        /// </summary>
        public int MaxTransaction  // ALARM !!! Find out what is the unit of performance for the login, or the exchange will select the entire deposit for fines / АХТУНГ!!! Узнай что такое единица производительности для логина, или биржа отберёт ВЕСЬ депозит за штрафы
        {
            get { return Limit; }
            set { Limit = value; }
        }

        /// <summary>
        /// Московское время
        /// </summary>
        static DateTime GetTimeMoscowNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc
            (DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"));
        }

        public static bool IsMoexClosed
        {
            get
            {
                var now = GetTimeMoscowNow().TimeOfDay;
                return now < TimeSpan.FromHours(10) || now > TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(50));
            }
        }

        public static DateTimeOffset ClearingStart => new(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 13, 45, 0, TimeSpan.FromHours(3));
        public static DateTimeOffset ClearingEnd => new(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 14, 15, 0, TimeSpan.FromHours(3));

        public DateTime StartOfConnector { get; private set; }

        /// <summary>
        /// Событие выполненного ордера
        /// </summary>
        public event Action<Order> NewOrderFilled;
        
        /// <summary>
        /// Order и сообщение от биржи
        /// </summary>
        public event Action<Order, string?> NewCanceledOrder;

        public event Action<Order> NewActiveOrder;


        /// <summary>
        /// initialization string connection to the router
        /// строка инициализации подключения к Роутеру
        /// </summary>
        private string GateOpenString; // = "ini=Plaza/PlazaStartSettings.ini;key=11111111"; 

        private string ConnectionOpenString;


        //static string testConnectionString = "p2tcp://127.0.0.1:4001;app_name=FinAlgoTrader;name=connectorPrime";
        //static string testConnectionString = "p2tcp://127.0.0.1:4001;app_name=FinAlgoTrader;name=connectorPrime";
        //static string realConnectionString = "p2lrpcq://127.0.0.1:4001;app_name=FinAlgoTrader;name=connectorPrime";


        
        public ConcurrentDictionary<string, PositionOnBoard> Positions = new ConcurrentDictionary<string, PositionOnBoard>();
        public ConcurrentDictionary<string, Portfolio> Portfolios = new ConcurrentDictionary<string, Portfolio>();
        public Portfolio Portfolio { get
            {

                if (Emulation)
                    return new Portfolio()
                    {
                        Number = "EmulationPortfolio",
                        ValueBegin = 20000,
                    };
                return Portfolios.Single().Value;

            } }

        /// <summary>
        /// сюда записываю поступающие из бэка ордера
        /// </summary>
        public ConcurrentDictionary<int, Order> Orders = new ConcurrentDictionary<int, Order>();

        /// <summary>
        /// key Order.NumberUser, а Value для Order.NumberMarket
        /// </summary>
        //public ConcurrentDictionary<int, string> OrdersHash = new ();

        //private TickEmulator tickEmulator;
        private DepthEmulator depthEmulator;

        private DepthPlaza depthPlaza;



        public TicksPlaza ticksplaza { get; set; }

        /// <summary>
        /// maximum number of transactions per second for login/максимальное количество транзакций в секунду для логина
        /// taken into account when parsing the queue for placing/cancellation orders/учитывается при разборе очереди на выставление/снятие заявок
        /// if you set the wrong, your deposit will disappear / если выставить не то, Ваш депозит уйдёт в зрительный зал
        /// </summary>


        /// <summary>
        /// Контроль трафика, приведенный к 100 милисекундам.
        /// </summary>
        private RateGate rateGateOrderWork;

        private string KeyFromFinalgoTrader { get; set; }

        public bool Emulation { get; set; }

		/// <summary>
		/// Создать соединение с биржой через коннектор PLAZA
		/// </summary>
		/// <param name="key">Ключ программы, выдается MOEX.</param>
		/// <param name="emulation">Настоящего подключения не будет</param>
		/// <param name="testTrading">тестовый ли вариант?</param>
		/// <param name="appname">название программы для подключения, соответствующий ключу</param>
		/// <param name="TickEventPeriodMilliSecond">Периодичность отправки тиков, миллисекунд</param>
		/// <param name="depthEventMillisecond"></param>
		public PlazaConnector(string key, bool emulation, bool testTrading = true, string appname = "", 
            int TickEventPeriodMilliSecond = 20,int depthEventMillisecond = 100)
        {
            AddPlazaDll();

            IsTestRegim = testTrading; //IsTestRegim = true; // тестовый

            if (IsTestRegim)
            {
                appname = "ntest_send";
                key = "11111111";
            }

            Emulation = emulation;

            if (MaxTransaction == 0)
                   MaxTransaction = 30;
           
            rateGateOrderWork = new RateGate((int)Math.Floor(MaxTransaction / 1m), TimeSpan.FromMilliseconds(1000));

            depthEmulator = new DepthEmulator();
            depthEmulator.MarketDepthChanged += (md) => { MarketDepthChangeEvent?.Invoke(md); };
            depthEmulator.NewTickCollectionEvent += (t) => NewTickCollectionEvent?.Invoke(t);

            depthPlaza = new DepthPlaza(this, depthEventMillisecond);

            ticksplaza = new TicksPlaza(this, TickEventPeriodMilliSecond);
            ticksplaza.NewTickCollectionEvent += (t) => NewTickCollectionEvent?.Invoke(t);
            ticksplaza.AllTicksLoadedEvent += () => TicksLoadedEvent?.Invoke();


            _statusNeeded = ServerConnectStatus.Disconnect;
            Status = ServerConnectStatus.Disconnect;
            _ordersToExecute = new Queue<Order>();
            _ordersToCansel = new Queue<Order>();

            GateOpenString = "ini=SchemasPlaza/PlazaStartSettings.ini;key=" + key;

            ConnectionOpenString = $"p2lrpcq://127.0.0.1:4001;app_name={appname};name=connectorPrime";

            //GateOpenString = $"ini={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SchemasPlaza\\PlazaStartSettings.ini")};key=" + key;

            //p2tcp://127.0.0.1:4001;app_name=send_mt", "p2mqreply://;ref=srvlink0"

            if (testTrading)
                ConnectionOpenString = "p2tcp://127.0.0.1:4001;app_name=ntest_send";
        }

        private static void AddPlazaDll()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                string assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RouterPlaza", new AssemblyName(eventArgs.Name).Name + ".dll");
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
                return null;
            };
        }

        internal bool IsTestRegim;

        public bool LoadTicksFromStart { get; set; }

        /*
        //добавил самостоятельно, чтобы упростить подписку и разброс котировок
        //должен в параллеле раскидывать котировки
        private void PlazaClient_MarketDepthChangeEvent(MarketDepth marketDepth)
        {
            if (SecuritiesQuotes.ContainsKey(marketDepth.SecurityId))
                SecuritiesQuotes[marketDepth.SecurityId].UpdateBidsAsks(marketDepth);
        }*/

        public ObservableCollection<Order> ActiveOrders = new ObservableCollection<Order>();

        /// <summary>
        /// Словарь для обновления котировок
        /// Не очень хорошо, что он использует просто String
        /// По хорошему надо использовать ID
        /// И вообще лучше в INT
        /// </summary>
        ConcurrentDictionary<string, Security> SecuritiesQuotes = new ConcurrentDictionary<string, Security>();

        object orderlock = new object();

        /// <summary>
        /// немного награмождено
        /// можно сделать быстрее
        /// </summary>
        /// <param name="order"></param>
        private async void PlazaClient_NewMyOrderEvent(Order order)
        {
            lock (orderlock)
            {
                if (order.State == Order.OrderStateType.Activ)
                {
                    ActiveOrders.Add(order);
                }
                if (order.State == Order.OrderStateType.Cancel)
                    ActiveOrders.Remove(order);

                if (order.State == Order.OrderStateType.Partial)
                {
                    //Debug.WriteLine("Выполнено ордер частично {0} {1} {2} обьем = {3}", order.NumberMarket, order.Price, order.Side, order.VolumeExecute);
                }

                if (order.State == Order.OrderStateType.Done)
                {
                    //  Debug.WriteLine("Ордер Исполнен {0} {1} {2} обьем = {3}", order.NumberMarket, order.Price, order.Side, order.VolumeExecute);
                    //возможно ордер сразу исполниться
                    //тогда мы не найдем его в списке
                    ActiveOrders.Remove(order);

                }
            }
        }

        /// <summary>
        /// Моя дополнительная реализация
        /// Чтобы автоматически обновлять у инструмента котировки
        /// </summary>
        /// <param name="security"></param>
        private void RegisterMarketDepth(Security security)
        {
            if (security == null)
            {
                return;
            }
            if (SecuritiesQuotes.TryAdd(security.Id, security))
                StartMarketDepth(security);

            depthPlaza.Subscription(security.Id);
        }


        /// <summary>
        /// Метод который два в одном и регистрация и отписка Стакана.
        /// </summary>
        /// <param name="register">true - подписать, false - отписать </param>
        public void Register_Unregister_MarketDepth(string secid, bool register)
        {
            try
            {

                if (!Securities.ContainsKey(secid) || Securities[secid] == null)
                    return ;

                var sec = Securities[secid];

                if (register)
                {
                    RegisterMarketDepth(sec, Emulation);
                }
                else
                {

                    UnRegisterMarketDepth(sec, Emulation);
                }

                return ;
            }
            catch (Exception ex)
            {
                return;
                //NOTE: продумать реализацию.
            }
        }


        /// <summary>
        /// Подписка на получение стакана, с эмуляцией
        /// </summary>
        /// <param name="security"></param>
        /// <param name="emulatorIsOn"></param>
        public void RegisterMarketDepth(Security security, bool emulatorIsOn  )
        {
            if (emulatorIsOn)
            {
                depthEmulator.Subscribe(security);
            }
            else
            {
                RegisterMarketDepth(security);
            }
        }

        /// <summary>
        /// Остановить обновление котировок у инструмента
        /// </summary>
        /// <param name="security"></param>
        public void UnRegisterMarketDepth(Security security, bool emulatorIsOn)
        {
            if (emulatorIsOn)
            {
                depthEmulator.UnSubscribe(security);
            }
            else
            {
                SecuritiesQuotes.TryRemove(security.Id, out _);
                StopMarketDepth(security);
            }
        }



        // Подписка на тики
        /// <summary>
        /// Зарегестрированные тики
        /// </summary>
        public HashSet<string> RegisteredTicks { get; private set; } =  new HashSet<string>();

        /// <summary>
        /// Метод который два в одном и регистрация и отписка
        /// </summary>
        /// <param name="register">true - подписать, false - отписать </param>
        public void Register_Unregister_Ticks(string secid, bool register)
        {
            try
            {

                if (!Securities.ContainsKey(secid) || Securities[secid] == null)
                    return;

                var sec = Securities[secid];

                if (register)
                {
                    TryRegisterTicks(sec,Emulation);
                }
                else
                {
                    UnRegisterTicks(sec, Emulation);
                }

            }
            catch (Exception ex)
            {

                //NOTE: продумать реализацию.
            }
        }

        /// <summary>
        /// Подписка на получение тиков по инструменту
        /// Если уже добавлен в список, то ничего не произойдет. 
        /// </summary>
        /// <param name="security"></param>
        public void TryRegisterTicks(Security security,bool emulation)
        {
            if (emulation)
            {
                depthEmulator.SubscribeTick(security);
            }
            else
            {

                if (RegisteredTicks.Add(security.Id))
                    SendLogMessage($"Tick Registered {security.Id}");
            }
        }

        /// <summary>
        /// Отписка от получения тиков по инструменту
        /// </summary>
        /// <param name="security"></param>
        public void UnRegisterTicks(Security security, bool emulation)
        {
            if (emulation)
            {
                depthEmulator.UnSubscribeTick(security);
            }
            else
            {
                if (RegisteredTicks.Contains(security.Id))
                {
                    RegisteredTicks.Remove(security.Id);
                    SendLogMessage($"Tick UnRegistered {security.Id}");
                }
            }
        }



        // connection settings constants
        // константы настроек соединения


        //обычная строка 



        /// <summary>
        /// боевая строка
        /// connector connection string. Initializes the connection to the router through shared memory./строка подключения коннектора. Инициализирует подключение к Роутеру через разделяемую память
        /// if such a connection is not closed normally, you will have to go to the task manager and wet the P2MQRouter * 32/в случае, если такое соединение закрылось не штатно, придётся идти в диспетчер задач и мочить P2MQRouter*32
        /// from the C drive, launch the Router via start_router.cmd/далее из диска С запускать Роутер через start_router.cmd
        /// </summary>
        //private const string ConnectionOpenString = "p2lrpcq://127.0.0.1:4001;app_name=osaApplication;name=connectorPrime";

        /// <summary>
        /// initialization line of the Listner responsible for receiving information about the instruments
        /// строка инициализации Листнера отвечающего за приём информации об инструментах
        /// </summary> //refdata2
        // private const string ListenInfoString = "p2repl://FORTS_REFDATA_REPL;scheme=|FILE|Plaza/Schemas/fut_info.ini|CustReplScheme";
        private const string ListenInfoString = "p2repl://FORTS_REFDATA_REPL;scheme=|FILE|SchemasPlaza/Schemas/refdata.ini|CustReplScheme";

        /// <summary>
        /// initialization string of the Listner responsible for receiving position
        /// строка инициализации листнера отвечающего за приём позиции
        /// </summary>
        //private const string ListenPositionString = "p2repl://FORTS_POS_REPL;scheme=|FILE|Plaza/Schemas/portfolios.ini|CustReplScheme";
        private const string ListenPositionString = "p2repl://FORTS_POS_REPL;scheme=|FILE|SchemasPlaza/Schemas/pos.ini|CustReplScheme";

        /// <summary>
        /// initialization string of the Listner responsible for getting portfolios
        /// строка инициализации листнера отвечающего за приём портфелей
        /// </summary>
        private const string ListenPortfolioString = "p2repl://FORTS_PART_REPL;scheme=|FILE|SchemasPlaza/Schemas/part.ini|CustReplScheme";

       

        /// <summary>
        /// initialization string of the Listner responsible for receiving my trades and my orders
        /// строка инициализации листнера отвечающего за приём Моих сделок и моих ордеров
        /// </summary>
        //private const string ListenOrderAndMyTradesString = "p2repl://FORTS_TRADE_REPL;scheme=|FILE|Plaza/Schemas/fut_trades.ini|CustReplScheme";
        private const string ListenOrderAndMyTradesString = "p2repl://FORTS_TRADE_REPL;scheme=|FILE|SchemasPlaza/Schemas/trades.ini|CustReplScheme";

        /// <summary>
        /// initialization string of the Listner responsible for receiving depth
        /// строка инициализации для листнера отвечающего за приём среза стакана
        /// </summary>
        private const string ListenMarketDepth = "p2repl://FORTS_AGGR50_REPL;scheme=|FILE|SchemasPlaza/Schemas/orders_aggr.ini|CustReplScheme";

        /// <summary>
        /// Обеличенная книга заявок, пока не используется
        /// </summary>
        private const string ListenUserOrderBook = "p2repl://FORTS_USERORDERBOOK_REPL;scheme=|FILE|SchemasPlaza/Schemas/userorderbook.ini|CustReplScheme";

        /// <summary>
        /// initialization string for order publisher
        /// строка инициализации для публишера ордеров
        /// </summary>

        // "p2mq://FORTS_SRV;category=FORTS_MSG;name=srvlink;timeout=5000;scheme=|FILE|forts_messages.ini|message"
        private const string PublisherOrdersString =
            "p2mq://FORTS_SRV;category=FORTS_MSG;name=srvlink;timeout=5000;scheme=|FILE|SchemasPlaza/Schemas/forts_messages.ini|message";






        /// <summary>
        /// initialization string for transaction tracing list listener
        /// строка инициализации для листнера следящего за реакцией публишера сделок
        /// </summary>
        private const string ListnerOrders = "p2mqreply://;ref=srvlink";

        // server status
        // статус сервера

        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
		/// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus Status
        {
            get { return _serverConnectStatus; }
            set //заменил private set на set чтобы проверить
            {
                if (value == _serverConnectStatus)
                {
                    return;
                }
                _serverConnectStatus = value;

                if (ConnectStatusChangeEvent != null)
                {
                    ConnectStatusChangeEvent(_serverConnectStatus);
                }
            }
        }

        /// <summary>
        /// user-ordered status/статус заказанный пользователем
        /// if it is different from the current server status/если он будет отличаться от текущего статуса сервера
        /// main thread will try to bring them to the same value/основной поток будет пытаться привести их к одному значению
        /// </summary>
        private ServerConnectStatus _statusNeeded;

        /// <summary>
		/// called when the server status changes
        /// вызывается при изменении статуса сервера
        /// </summary>
        public event Action<ServerConnectStatus> ConnectStatusChangeEvent;

        // connection setup and listening
        // установка соединения и слежение за ним

        /// <summary>
        /// object to connect to router
        /// объект подключения к Роутеру
        /// </summary>
        public Connection ConnectionObjectPlaza;

        /// <summary>
		/// thread responsible for connecting to Plaza, monitoring threads and processing incoming data
        /// поток отвечающий за соединение с плазой, следящий за потоками и обрабатывающий входящие данные
        /// </summary>
        private Thread _threadPrime;



        /// thread watching the main thread/поток присматривающий за основным потоком
        /// and if the main thread does not respond, more than a minute/и если основной поток не отвечает, больше минуты
        /// reconnects the entire system/переподключает всю систему
        /// </summary>
        private Thread _threadNanny;

        /// <summary>
		/// multi-threaded access locker to Plaza object
        /// объект участвующий в блокировке многопоточного доступа к объектам Плаза
        /// </summary>
        private object _plazaThreadLocker;

        /// <summary>
		/// start Plaza server
        /// запустить сервер Плаза
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public void Connect()
        {
            _statusNeeded = ServerConnectStatus.Connect;

            //CheckConnectionAndLicense(KeyFromFinalgoTrader);


            if (_threadPrime != null)
            {
                return;
            }
            try
            {
                //дописал, чтобы кидать события только новых выполненных ордеров
                StartOfConnector = GetTimeMoscowNow();

                //Добавляю сразу свои события в коннектор
                //this.MarketDepthChangeEvent += PlazaClient_MarketDepthChangeEvent;
                //this.NewMyOrderEvent += PlazaClient_NewMyOrderEvent;
                //--------------------------------------------------

                _plazaThreadLocker = new object();

                CGate.Open(GateOpenString);
                
                ConnectionObjectPlaza = new Connection(ConnectionOpenString);
                
                _statusNeeded = ServerConnectStatus.Connect;

                _threadPrime = new Thread(PrimeWorkerThreadSpace);
                _threadPrime.Name = "PrimeWorkerThreadSpace";
                _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
                _threadPrime.IsBackground = true;
                _threadPrime.Start();

                _threadNanny = new Thread(ThreadNannySpace);
                _threadNanny.Name = "ThreadNannySpace";
                _threadNanny.CurrentCulture = new CultureInfo("ru-RU");
                _threadNanny.IsBackground = true;
                _threadNanny.Start();

                _heartBeatSenderThread = new Thread(HeartBeatSender);
                _heartBeatSenderThread.Name = "HeartBeatSender";
                _heartBeatSenderThread.CurrentCulture = new CultureInfo("ru-RU");
                _heartBeatSenderThread.IsBackground = true;
                _heartBeatSenderThread.Start();

                _threadOrderExecutor = new Thread(ExecutorOrdersThreadArea);
                _threadOrderExecutor.Name = "ExecutorOrdersThreadArea";
                _threadOrderExecutor.CurrentCulture = new CultureInfo("ru-RU");
                _threadOrderExecutor.IsBackground = true;
                _threadOrderExecutor.Start();


            }
            catch (Exception error)
            {
                SendLogMessage(error);
            }
        }

        private bool lisenceon = false;


        /// <summary>
        /// stop Plaza server
        /// остановить сервер Плаза
        /// </summary>
        public void Stop()
        {
            Thread worker = new Thread(Dispose);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Start();

            Thread.Sleep(2000);
        }

        /// <summary>
		/// clear all objects involved in the connection with Plaza 2
        /// очистить все объекты участвующие в соединение с Плаза 2
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public void Dispose()
        {
            _statusNeeded = ServerConnectStatus.Disconnect;
            Thread.Sleep(1000);

            // turn off thread watching the main thread/отключаем поток следящий за основным потоком
            if (_threadNanny != null)
            {
                try
                {
                    _threadNanny.Name = "deleteThread";
                    //_threadNanny.Abort();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }
            }

            _threadNanny = null;

            // turn off the main thread/отключаем основной поток
            if (_threadPrime != null)
            {
                try
                {
                    _threadPrime.Name = "deleteThread";
                    //_threadPrime.Abort();
                    Thread.Sleep(500);
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }
            }
            _threadPrime = null;


            // turn off the heartbeat thread/ отключаем поток отправляющий хартбиты
            if (_heartBeatSenderThread != null)
            {
                try
                {
                    _heartBeatSenderThread.Name = "deleteThread";
                    //_heartBeatSenderThread.Abort();
                    Thread.Sleep(500);
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }
            }
            _heartBeatSenderThread = null;

            // turn off thread sending orders from queue/ отключаем поток отправляющий заявки из очереди
            if (_threadOrderExecutor != null)
            {
                try
                {
                    _threadOrderExecutor.Name = "deleteThread";
                    //_threadOrderExecutor.Abort();
                    Thread.Sleep(500);
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }
            }
            _threadOrderExecutor = null;

            // disconnect / отключаем соединение
            if (ConnectionObjectPlaza != null)
            {
                try
                {
                    ConnectionObjectPlaza.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }
                ConnectionObjectPlaza = null;
            }
            // turn off listeners / отключаем листнеры

            if (_listenerInfo != null)
            {
                try
                {
                    if (_listenerInfo.State == State.Active)
                    {

                        _listenerInfo.Close();
                    }
                    _listenerInfo.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }

                _listenerInfoNeadToReconnect = false;
                _listenerInfo = null;
            }
            if (_listenerMarketDepth != null)
            {
                try
                {
                    if (_listenerMarketDepth.State == State.Active)
                    {
                        _listenerMarketDepth.Close();
                    }
                    _listenerMarketDepth.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }

                _listenerMarketDepthNeadToReconnect = false;
                _listenerMarketDepth = null;
            }

            if (_listenerOrderAndMyDeal != null)
            {
                try
                {
                    if (_listenerOrderAndMyDeal.State == State.Active)
                    {
                        _listenerOrderAndMyDeal.Close();
                    }
                    _listenerOrderAndMyDeal.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }

                _listenerOrderAndMyDealNeadToReload = false;
                _listenerOrderAndMyDeal = null;
            }

            if (_listenerPosition != null)
            {
                try
                {
                    if (_listenerPosition.State == State.Active)
                    {
                        _listenerPosition.Close();
                    }
                    _listenerPosition.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }
                _listenerPositionNeadToReconnect = false;
                _listenerPosition = null;
            }

            if (_listenerPortfolio != null)
            {
                try
                {
                    if (_listenerPortfolio.State == State.Active)
                    {
                        _listenerPortfolio.Close();
                    }
                    _listenerPortfolio.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }
                _listenerPortfolioNeadToReconnect = false;
                _listenerPortfolio = null;
            }

            ticksplaza?.Dispose();
            

            if (_publisher != null)
            {
                try
                {
                    if (_publisher.State == State.Active)
                    {
                        _publisher.Close();
                    }
                    _publisher.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }

                _publisher = null;
            }

            if (_listenerOrderSendMirror != null)
            {
                try
                {
                    if (_listenerOrderSendMirror.State == State.Active)
                    {
                        _listenerOrderSendMirror.Close();
                    }
                    _listenerOrderSendMirror.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error);
                }

                _listenerOrderSendMirrorNeadToReload = false;
                _listenerOrderSendMirror = null;

            }

            // close connection with router / закрываем соединение с роутером
            try
            {
                CGate.Close();
            }
            catch (Exception error)
            {
                SendLogMessage(error);
            }
        }

        /// <summary>
		/// reconnect
        /// переподключиться
        /// </summary>
        private async void ReconnectPlaza()
        {
            await Task.Factory.StartNew(() => 
            {
                Status = ServerConnectStatus.Disconnect;
                _lastMoveTime = GetTimeMoscowNow(); // отмечаем флаг нахождения нового потока
                Stop();
                Dispose();
                Connect();
            });
        }

        /// <summary>
		/// time of the last checkpoint of main thread
        /// время последнего чекПоинта основного потока
        /// </summary>
        private DateTime _lastMoveTime = DateTime.MinValue;

        /// <summary>
		/// place of work of stream 2, which looks after the responses of stream 1
        /// место работы потока 2, который присматривает за откликами потока 1
        /// </summary>
        private void ThreadNannySpace()
        {
            while (Thread.CurrentThread.Name != "deleteThread")
            {
                Thread.Sleep(2000);

                if (_lastMoveTime != DateTime.MinValue &&
                    _lastMoveTime.AddMinutes(1) < GetTimeMoscowNow())
                {
                    SendLogMessage("Прервался рабочий поток");
                    ReconnectPlaza();
                    return;
                }
            }
        }

        /// <summary>
		/// place of main stream work
        /// место работы основного потока
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        private void PrimeWorkerThreadSpace()
        {
            try
            {

                while (Thread.CurrentThread.Name != "deleteThread")
                {
                    
                    //lock (_plazaThreadLocker)
                    {
                        _lastMoveTime = GetTimeMoscowNow();

                        State connectionState = ConnectionObjectPlaza.State;

                        if (connectionState == State.Closed &&
                            _statusNeeded == ServerConnectStatus.Connect)
                        {
                            try
                            {
                                StartRouterPlaza();
                                Thread.Sleep(1000);
                                ConnectionObjectPlaza.Open("");
                                RouterLogger.Log("Запрос подключения отправлен!","Connect");
                                continue;
                            }
                            catch (Exception error)
                            {
                                RouterLogger.Log($"Не удалось подключиться! {error.Message}", "Connect");
                                CloseRouterPlaza(false);
                                // could not connect/не получилось подключиться
                                SendLogMessage(error);
                                Thread.Sleep(10000);
                                StartRouterPlaza();
                                try
                                {
                                    Thread.Sleep(10000);
                                    //in ten seconds we try again/через десять секунд пытаемся ещё раз
                                    ConnectionObjectPlaza.Open("");
                                }
                                catch (Exception error2)
                                {
                                    RouterLogger.Log($"Повторно Не удалось подключиться! {error2.Message}", "Connect");
                                    CloseRouterPlaza(true);
                                    ReconnectPlaza(); 
                                    return;
                                }
                                continue;
                            }
                        }
                        else if (connectionState == State.Opening)
                        {
                            // ignore
                        }
                        else if (connectionState == State.Active &&
                                 _statusNeeded == ServerConnectStatus.Disconnect)
                        {
                            try
                            {
                                ConnectionObjectPlaza.Close();
                                //ConnectionObjectPlaza.Dispose();
                            }
                            catch (Exception error)
                            {
                                SendLogMessage(error);
                            }

                            Status = ServerConnectStatus.Disconnect;
                            continue;
                        }
                        if (connectionState == State.Error)
                        {
                            try
                            {
                                ConnectionObjectPlaza.Close();
                            }
                            catch
                            {
                                ReconnectPlaza();
                            }
                            continue;
                        }
                        if (connectionState != State.Active)
                        {
                            continue;
                        }

                        // connect listeners/подключаем листнеры

                        CheckListnerInfo(); // instruments and portfolios/инструменты и портфели

                        CheckListnerPosition(); //listening to positions on portfolios/прослушка позиции по портфелям

                        ticksplaza.CheckListnerTrades(); //listening to deals and applications/прослушка сделки и завки
                        
                        CheckListnerMyTrades(); //listening to order log and my trades/прослушка ордер лог и мои сделки

                        CheckListnerMarketDepth(); // depth/стакан

                        // publisher/публишер 
                        CheckOrderSender();

                        // look at the data/смотрим очедедь данных

                        if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                        {
                            continue;
                        }

                        ConnectionObjectPlaza.Process(1);
                    }

                   // Thread.Sleep(60000);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error);
                // set the time of the last review from the main thread - four minutes ago/назначаем время последнего отзыва от основного потока - четырем минуты назад
                // thereafter, the stream watching the main stream starts the reconnection procedure/после этого поток следящий за основным потоком начинает процедуру переподключения
                _lastMoveTime = GetTimeMoscowNow().AddMinutes(-4);
            }
        }

        /// <summary>
        /// Остановка внешего приложения Роутер Plaza
        /// </summary>
        private void CloseRouterPlaza(bool HardKill)
        {
            var processRouter = Process.GetProcessesByName("P2MQRouter64");
            foreach (Process p in processRouter)
                if (HardKill)
                    p.Kill();
                else
                    p.CloseMainWindow();
        }

        /// <summary>
        /// Запуск внешнего приложения Роутер Plaza
        /// </summary>
        private void StartRouterPlaza()
        {

            if (Emulation)
                return;

           // return; //тест

            var processRouter = Process.GetProcessesByName("P2MQRouter64");
            if ((processRouter?.Length??0) == 0)
            {
                ProcessStartInfo procPlaza = new ProcessStartInfo();
                procPlaza.UseShellExecute = true;
                var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                procPlaza.WorkingDirectory = $"{directoryName}\\RouterPlaza";



#if DEBUG
                if (IsTestRegim)
                {
                    procPlaza.FileName = "start_router64TestDebug.cmd";
                }
                else
                {
                    procPlaza.FileName = "start_router64RealDebug.cmd";
                }
                
#else
                if (IsTestRegim)
                {
                    procPlaza.FileName = "start_router64TestRelease.cmd";
                }
                else
                {
                    procPlaza.FileName = "start_router64RealRelease.cmd";
                }
                
#endif

                Process.Start(procPlaza);
            }
        }

        // Instruments. thread FORTS_FUTINFO_REPL table "fut_sess_contents"
        // Инструменты. поток FORTS_FUTINFO_REPL таблица "fut_sess_contents"

        /// <summary>
        /// listner responsible for accepting tools
        /// листнер отвечающий за приём инструментов
        /// </summary>
        private Listener _listenerInfo;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerInfoNeadToReconnect;

        /// <summary>
		/// thread state of instrument listener
        /// состояние потока листнера инструментов
        /// </summary>
        /// </summary>
        private string _listenerInfoReplState;

        /// <summary>
		/// check the instrument listener for connection and validity
        /// проверить на подключение и валидность листнер инструментов
        /// </summary>
        private void CheckListnerInfo()
        {
            try
            {
                if (_listenerInfo == null)
                {
                    _listenerInfo = new Listener(ConnectionObjectPlaza, ListenInfoString);
                    _listenerInfo.Handler += ClientMessage_Board;
                }
                if (_listenerInfoNeadToReconnect || _listenerInfo.State == State.Error)
                {


                    _listenerInfoNeadToReconnect = false;
                    try
                    {

                        _listenerInfo.Close();
                        _listenerInfo.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }

                    _listenerInfo = null;
                    return;
                }

                if (_listenerInfo.State == State.Closed)
                {
                    if (_listenerInfoReplState == null)
                    {
                        _listenerInfo.Open("mode=snapshot+online");
                        // _listenerInfo.Open("");
                    }
                    else
                    {
                        _listenerInfo.Open("");
                        // _listenerInfo.Open();
                        // _listenerInfo.Open("mode=snapshot+online;" + "replstate=" + _listenerInfoReplState);
                    }
                }
            }
            catch (Exception error)
            {
                try
                {
                    _listenerInfo.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerInfo = null;

                SendLogMessage(error);
            }
        }

        /// <summary>
		/// handles the tool table here
        /// здесь обрабатывается таблица инструментов 
        /// </summary>
        public int ClientMessage_Board(
            Connection conn, Listener listener, Message msg)
        {

            //Debug.WriteLine(msg.Type);
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            RouterLogger.Log(replmsg.ToString(), "ClassSec");
                            if (replmsg.MsgName == "fut_sess_contents")
                            {
                                try
                                {
                                    var security = new Security(
                                        replmsg["short_isin"].asUnicodeString(),
                                        replmsg["name"].asUnicodeString(),
                                        SecurityType.Futures,
                                        "FORTS",
                                        //NOTE: Почему то не открыто?
                                        1) //Convert.ToDecimal(replmsg["lot_volume"].asInt());
                                    {
                                        Id = replmsg["isin_id"].asInt().ToString(),
                                        Name = replmsg["isin"].asUnicodeString(),
                                        PriceStep = Convert.ToDecimal(replmsg["min_step"].asDecimal()),
                                        PriceStepCost = Convert.ToDecimal(replmsg["step_price"].asDecimal()),
                                        // From Spectra 6.5 "last_cl_quote" was deleted
                                        PriceLimitLow = Convert.ToDecimal(replmsg["settlement_price"].asDecimal()) - Convert.ToDecimal(replmsg["limit_down"].asDecimal()),
                                        PriceLimitHigh = Convert.ToDecimal(replmsg["settlement_price"].asDecimal()) + Convert.ToDecimal(replmsg["limit_up"].asDecimal()),
                                    };

                                    Debug.WriteLine($"Добавлен ключ {security.Name}");
                                    Securities[security.Id] = security;

                                    UpdateSecurity?.Invoke(security);
                                    _securities[security.Name] = security.Id;
                                }

                                catch (Exception error)
                                {
                                    SendLogMessage(error);
                                }

                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerInfoReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerInfoNeadToReconnect = true;
                            break;
                        }
                    case MessageType.MsgP2ReplOnline:
                        {
                            SecuritiesLoadedEvent?.Invoke();
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
        }

        public Security? GetSecurityByIsin(string isin)
        {
            Securities.TryGetValue(isin, out var security);
            return security;
        }

        /// <summary>
		/// called when a new instrument appears
        /// вызывается при появлении нового инструмента
        /// </summary>
        public event Action<Security> UpdateSecurity;

        /// <summary>
        /// Все инструменты загружены и вышли в режим Онлайн
        /// </summary>
        public event Action SecuritiesLoadedEvent;

        // Positions and portfolios. thread FORTS_POS_REPL and tables "position" and "part"
        // Позиции и портфели. поток FORTS_POS_REPL и c таблица "position" и "part"

        /// <summary>
        /// portfolio listener
        /// листнер портфелей
        /// </summary>
        private Listener _listenerPortfolio;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerPortfolioNeadToReconnect;

        /// <summary>
		/// position listener
        /// листнет позиций
        /// </summary>
        private Listener _listenerPosition;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerPositionNeadToReconnect;

        /// <summary>
		/// thread state of position listener
        /// состояние потока листнера позиций
        /// </summary>
        private string _listenerPositionReplState;

        /// <summary>
		/// thread state of portfolio listener
        /// состояние потока листнера портфелей
        /// </summary>
        private string _listenerPortfolioReplState;

        /// <summary>
		/// check the position and portfolio listener for connection and validity
        /// проверить на подключение и валидность листнер позиций и портфелей
        /// </summary>
        private void CheckListnerPosition()
        {
            try
            {
                if (_listenerPosition == null)
                {
                    _listenerPosition = new Listener(ConnectionObjectPlaza, ListenPositionString);
                    _listenerPosition.Handler += ClientMessage_Position;
                }
                if (_listenerPositionNeadToReconnect || _listenerPosition.State == State.Error)
                {
                    _listenerPositionNeadToReconnect = false;
                    try
                    {
                        _listenerPosition.Close();
                        _listenerPosition.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }

                    _listenerPosition = null;
                    return;
                }
                if (_listenerPosition.State == State.Closed)
                {
                    if (_listenerPositionReplState == null)
                    {
                        _listenerPosition.Open("mode=snapshot+online");
                    }
                    else
                    {
                        _listenerPosition.Open("mode=snapshot+online;replstate=" + _listenerPositionReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerPosition.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerPosition = null;

                SendLogMessage(error);
            }
            try
            {
                if (_listenerPortfolio == null)
                {
                    _listenerPortfolio = new Listener(ConnectionObjectPlaza, ListenPortfolioString);
                    _listenerPortfolio.Handler += ClientMessage_Portfolio;
                }
                if (_listenerPortfolioNeadToReconnect || _listenerPortfolio.State == State.Error)
                {
                    _listenerPortfolioNeadToReconnect = false;
                    try
                    {
                        _listenerPortfolio.Close();
                        _listenerPortfolio.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }

                    _listenerPortfolio = null;
                    return;
                }
                if (_listenerPortfolio.State == State.Closed)
                {
                    if (_listenerPortfolioReplState == null)
                    {
                        _listenerPortfolio.Open("mode=snapshot+online");
                    }
                    else
                    {
                        _listenerPortfolio.Open("mode=snapshot+online;" + "replstate=" + _listenerPortfolioReplState);
                    }
                }
            }
            catch (Exception error)
            {
                try
                {
                    _listenerPortfolio.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerPortfolio = null;

                SendLogMessage(error);
            }
        }



        /// <summary>
        /// here process the table of positions by portfolios
        /// здесь обрабатывается таблица позиций по портфелям
        /// </summary>
        public int ClientMessage_Position(
            Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "position")
                            {
                                try
                                {
                                    PositionOnBoard positionOnBoard = new PositionOnBoard();

                                    positionOnBoard.SecurityId = replmsg["isin_id"].asInt().ToString();
                                    positionOnBoard.PortfolioName = replmsg["client_code"].asUnicodeString();
                                    positionOnBoard.ValueBegin = replmsg["xopen_qty"].asInt();
                                    positionOnBoard.XPosValueCurrent = replmsg["xpos"].asInt();
                                    positionOnBoard.ValueBlocked = positionOnBoard.XPosValueCurrent;

                                    Positions[positionOnBoard.SecurityId] = positionOnBoard;

                                    if (UpdatePosition != null)
                                    {
                                        UpdatePosition(positionOnBoard);
                                    }
                                }

                                catch (Exception error)
                                {
                                    SendLogMessage(error);
                                }

                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerPositionReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerPositionNeadToReconnect = true;
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

        }

        /// <summary>
		/// process the portfolio table here
        /// здесь обрабатывается таблица портфелей
        /// </summary>
        public int ClientMessage_Portfolio(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "part")
                            {
                                try
                                {
                                    if (_portfolios == null)
                                    {
                                        _portfolios = new List<Portfolio>();
                                    }

                                    string clientCode = replmsg["client_code"].asUnicodeString();

                                    Portfolio portfolio = _portfolios.Find(portfolio1 => portfolio1.Number == clientCode);

                                    if (portfolio == null)
                                    {
                                        portfolio = new Portfolio();
                                        portfolio.Number = clientCode;
                                        _portfolios.Add(portfolio);
                                    }
                                    Portfolios.TryAdd(portfolio.Number, portfolio);
                                    portfolio.ValueBegin = Convert.ToDecimal(replmsg["money_old"].asDecimal());
                                    portfolio.ValueCurrent = Convert.ToDecimal(replmsg["money_amount"].asDecimal());
                                    portfolio.ValueBlocked = Convert.ToDecimal(replmsg["money_blocked"].asDecimal());

                                    if (UpdatePortfolio != null)
                                    {
                                        UpdatePortfolio(portfolio);
                                    }
                                }

                                catch (Exception error)
                                {
                                    SendLogMessage(error);
                                }

                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerPortfolioReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerPortfolioNeadToReconnect = true;
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
        }

        /// <summary>
		/// called when changing position
        /// вызывается при изменении позиции
        /// </summary>
        public event Action<PositionOnBoard> UpdatePosition;

        /// <summary>
		/// called when new portfolios appear
        /// вызывается при появлении новых портфелей
        /// </summary>
        public event Action<Portfolio> UpdatePortfolio;

        /// <summary>
		/// portfolios in the system
        /// портфели в системе
        /// </summary>
        private List<Portfolio> _portfolios;

        // listening ticks. thread FORTS_DEALS_REPL "deal" table
        // Прослушка тиков. поток FORTS_DEALS_REPL таблица "deal"

       
       
        /// <summary>
		/// called when a new tick comes from the system
        /// вызывается когда из системы приходит новый тик
        /// </summary>
        public event Action<Trade> NewTickEvent;

        /// <summary>
		/// called when a new tick comes from the system
        /// вызывается когда из системы приходит пачка новых тиков
        /// </summary>
        public event Action<Dictionary<string,List<Trade>>> NewTickCollectionEvent;

        /// <summary>
        /// Все исторические тики загружены и вышли в режим оналйн
        /// </summary>
        public event Action TicksLoadedEvent;

        // depth thread FORTS_FUTAGGR20_REPL table "orders_aggr"
        // Стаканы поток FORTS_FUTAGGR20_REPL таблица "orders_aggr"

        /// <summary>
        /// depth listener
        /// листнер стаканов
        /// </summary>
        private Listener _listenerMarketDepth;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerMarketDepthNeadToReconnect;

        /// <summary>
		/// check connection and validity listener of depth
        /// проверить на подключение и валидность листнер среза стаканов
        /// </summary>
        private void CheckListnerMarketDepth()
        {
            try
            {
                if (_listenerMarketDepth == null)
                {
                    _listenerMarketDepth = new Listener(ConnectionObjectPlaza, ListenMarketDepth);
                    depthPlaza.SetListener(_listenerMarketDepth);
                    depthPlaza.MarketDepthChangeEvent += (md) =>
                    {
                         MarketDepthChangeEvent?.Invoke(md);
                    };
                    depthPlaza.MarketDepthLoadedEvent += () => MarketDepthLoadedEvent?.Invoke();

                }
                if (_listenerMarketDepthNeadToReconnect || _listenerMarketDepth.State == State.Error)
                {
                    _listenerMarketDepthNeadToReconnect = false;
                    try
                    {
                        _listenerMarketDepth.Close();
                        _listenerMarketDepth.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }

                    _listenerMarketDepth = null;
                    
                    return;
                }
                if (_listenerMarketDepth.State == State.Closed)
                {
                    _listenerMarketDepth.Open("mode=snapshot+online");
                    //_listenerMarketDepth.Open();
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerMarketDepth?.Dispose();
                }
                catch
                {
                    // ignore
                }
                
                _listenerMarketDepth = null;

                SendLogMessage(error);
            }
        }

        private Listener _listenerOrderBook;

        private bool _listenerOrderBookNeadToReconnect;


        /// <summary>
        /// instruments for which to collect depths
        /// инструменты по которым надо собирать стаканы
        /// </summary>
        private List<Security> _securitiesDepth;

        Dictionary<string, Security> RegisteredSecurities { get; } = [];

        /// <summary>
		/// start unloading depth on this instrument
        /// начать выгружать стакан по этому инструменту
        /// </summary>
        public void StartMarketDepth(Security security)
        { 
            RegisteredSecurities.Add(security.Id, security);
        }

        /// <summary>
		/// stop unloading depth on this instrument
        /// остановить выгрузку стакана по этому инструменту
        /// </summary>
        public void StopMarketDepth(Security security)
        {
            if (RegisteredSecurities.ContainsKey(security.Id))
                RegisteredSecurities.Remove(security.Id);

            depthPlaza.UnSubscription(security.Id);
        }

        /// <summary>
		/// class responsible for the assembly depth from the slice
        /// класс отвечающий за сборку стакана из среза
        /// </summary>
        //private PlazaMarketDepthCreator _depthCreator;

        /// <summary>
        /// depths that have been updated for the previous data series/стаканы которые обновились за предыдущую серию данных 
        /// and mailing these depths is required/и требуется рассылка этих стаканов
        /// </summary>
        private List<MarketDepth> rebildDepths;

        /// <summary>
        /// здесь ID - это имя инструмента
        /// </summary>
        Dictionary<string, MarketDepth> rebildDepths2 = new Dictionary<string, MarketDepth>();

       

        
        /// <summary>
		/// called when a glass has been updated.
        /// вызывается когда обновился какой-то стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthChangeEvent;

        /// <summary>
        /// Все стаканы загружены и вышли в режим Онлайн
        /// </summary>
        public event Action MarketDepthLoadedEvent;

        // my trades and orderLog thread FORTS_FUTTRADE_REPLL table "user_deal" "orders_log"
        // Мои сделки и ордерЛог поток FORTS_FUTTRADE_REPLL таблица "user_deal" "orders_log"

        /// <summary>
        /// listener of my deals and my orders
        /// листнер моих сделок и моих ордеров
        /// </summary>
        private Listener _listenerOrderAndMyDeal;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerOrderAndMyDealNeadToReload;

        /// <summary>
		/// thread state of my trades and my orders listener
        /// состояние потока листнера моих сделок и моих ордеров
        /// </summary>
        private string _listenerOrderAndMyDealReplState;

        /// <summary>
		/// check my orders and my trades listener for connection and validity
        /// проверить на подключение и валидность листнер моих ордеров и моих сделок
        /// </summary>
        private void CheckListnerMyTrades()
        {
            try
            {
                if (_listenerOrderAndMyDeal == null)
                {
                    _listenerOrderAndMyDeal = new Listener(ConnectionObjectPlaza, ListenOrderAndMyTradesString);
                    _listenerOrderAndMyDeal.Handler += ClientMessage_OrderAndMyDeal;
                }
                if (_listenerOrderAndMyDealNeadToReload || _listenerOrderAndMyDeal.State == State.Error)
                {
                    _listenerOrderAndMyDealNeadToReload = false;
                    try
                    {
                        _listenerOrderAndMyDeal.Close();
                        _listenerOrderAndMyDeal.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }

                    _listenerOrderAndMyDeal = null;
                    return;
                }
                if (_listenerOrderAndMyDeal.State == State.Closed)
                {
                    if (_listenerOrderAndMyDealReplState == null)
                    {
                        _listenerOrderAndMyDeal.Open("mode=snapshot+online");
                    }
                    else
                    {
                        _listenerOrderAndMyDeal.Open("mode=snapshot+online;" + "replstate=" + _listenerOrderAndMyDealReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerOrderAndMyDeal.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerOrderAndMyDeal = null;

                SendLogMessage(error);
            }
        }

        //получение ордера
        /// <summary>
		/// here my trades and orders table is processed
        /// здесь обрабатывается таблица мои сделки и ордеров
        /// </summary>
        public int ClientMessage_OrderAndMyDeal(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "user_deal")
                            {
                                try
                                {
                                    RouterLogger.Log($"{replmsg}", "user_deal");
                                    MyTrade trade = new MyTrade(replmsg);

                                    if (NewMyTradeEvent != null && trade.Time > StartOfConnector)
                                    {
                                        NewMyTradeEvent(trade);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error);
                                }
                            }
                            else if (replmsg.MsgName == "orders_log")
                            {
                                try
                                {
                                    RouterLogger.Log($"{replmsg}", "orders_log");

                                    int numberUser = replmsg["ext_id"].asInt();

                                    if (!Orders.TryGetValue(numberUser, out Order order))
                                    {
                                        order = new Order();
                                        order.timeSet = replmsg["moment"].asDateTime();
                                        order.volume = replmsg["public_amount"].asInt();
                                    }
                                    var oldState = order.state;
                                    order.SetNumberMarket(replmsg["public_order_id"].asUnicodeString());
                                    order.numberUser = replmsg["ext_id"].asInt();

                                    order.VolumeExecuted = order.volume - replmsg["public_amount_rest"].asInt(); // это у нас оставшееся в заявке
                                    order.SecIsin = Securities.ContainsKey(order.SecurityId) ? Securities[order.SecurityId].Name : "Empty";
                                    order.priceOrder = Convert.ToDecimal(replmsg["price"].asDecimal());
                                    order.PortfolioNumber = replmsg["client_code"].asString();
                                    order.SecurityId = replmsg["isin_id"].asInt().ToString();
                                    //security= Securities[_securities[replmsg["isin_id"].asInt().ToString()]], // справочник всех инструментов не успевает подгружаться до прихода события orders_log
                                    order.timeCallBack = replmsg["moment"].asDateTime();
                                    order.timeCreate = replmsg["moment"].asDateTime();
                                    //order.timeSet = Helper.GetTimeMoscowNow();
                                    order.Comment = replmsg["comment"].asString();
                                    

                                    int action = replmsg["private_action"].asInt();

                                    if (action == 0)
                                    {
                                        order.state = Order.OrderStateType.Cancel;
                                        order.timeCancel = order.TimeCallBack;
                                    }
                                    else if (action == 1)
                                    {
                                        order.state = Order.OrderStateType.Activ;
                                    }
                                    else if (action == 2 && replmsg["public_amount_rest"].asInt() == 0)
                                    {
                                        order.state = Order.OrderStateType.Done;
                                        order.timeDone = replmsg["moment"].asDateTime();
                                    }
                                    else if (action == 2 && replmsg["public_amount_rest"].asInt() != 0)
                                    {
                                        order.state = Order.OrderStateType.Partial;
                                        order.timeDone = replmsg["moment"].asDateTime();
                                    }
                                    else if(action ==3) // нет такого статуса в коннекторе
                                    {
                                        //order.state = Order.OrderStateType.Partial;
                                    }

                                    int dir = replmsg["dir"].asInt();

                                    if (dir == 1)
                                    {
                                        order.side = Side.Buy;
                                    }
                                    else if (dir == 2)
                                    {
                                        order.side = Side.Sell;
                                    }

                                    Orders[order.NumberUserOrderId] = order;

                                    //сразу первыми шлем событие если ордер обновился
                                    if (order.State == Order.OrderStateType.Done && order.TimeCallBack > StartOfConnector && NewOrderFilled != null)
                                        NewOrderFilled(order);

                                    if (order.State == Order.OrderStateType.Cancel && order.TimeCallBack > StartOfConnector  && NewCanceledOrder != null)
                                        NewCanceledOrder(order, default);

                                    if (order.State == Order.OrderStateType.Activ && order.TimeCallBack > StartOfConnector && NewActiveOrder != null)
                                        NewActiveOrder(order);
                                    

                                    Debug.WriteLine("ордер " + order.State + "/" + order.SecurityId);
                                    //это и есть событие обновления статуса ордера

                                    if (order.TimeCallBack > StartOfConnector)
                                    {
                                        if (oldState != order.state)
                                        {
                                            OrderChangedEvent?.Invoke(order, "The order has changed state.");
                                        }
                                    }

                                    RouterLogger.Log($"{order}", "orders_log");
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error);
                                }
                            }

                            break;
                        }
                    case MessageType.MsgP2ReplOnline:
                        {
                            OrderLoadedEvent?.Invoke();
                            break;
                        }
                    case MessageType.MsgTnBegin:
                        {
                            break;
                        }
                    case MessageType.MsgTnCommit:
                        {
                            break;
                        }
                    case MessageType.MsgOpen:
                        {
                            break;
                        }
                    case MessageType.MsgClose:
                        {
                            break;
                        }
                    case MessageType.MsgP2ReplLifeNum:
                        {
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerOrderAndMyDealReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerOrderAndMyDealNeadToReload = true;
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
        }

        /// <summary>
		/// called when a new trade comes from the system
        /// вызывается когда из системы приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        /// <summary>
		/// called when a new order comes from the system
        /// вызывается когда происходит изменение состояни ордера
        /// </summary>
        public event Action<Order, string?> OrderChangedEvent;

        /// <summary>
        /// Все ранее отправленные ордера загружены и вышли в режим Онлайн
        /// </summary>
        public event Action OrderLoadedEvent;

        // order execution
        // Исполнение заявок

        /// <summary>
        /// listener listening to publisher's answers (thing that transmit orders to the system)
        /// листнер прослушивающий ответы публишера(штуки которая передаёт ордера в систему)
        /// </summary>
        private Listener _listenerOrderSendMirror;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerOrderSendMirrorNeadToReload;

        /// <summary>
		/// thread status of listener monitoring the publisher
        /// состояние потока листнера следящего за публишером
        /// </summary>
        private string _listenerOrderSenderMirrorReplState;

        /// <summary>
		/// trades publisher and CODHeartbeat
        /// публишер сделок и CODHeartbeat
        /// </summary>
        private Publisher _publisher;

        /// <summary>
		/// check on the connection and validity of the publisher and listner watching his responses
        /// проверить на подключение и валидность публишер и листнер следящий за его откликами
        /// </summary>
        private void CheckOrderSender()
        {
            try
            {
                if (_publisher == null)
                {
                    _publisher = new Publisher(ConnectionObjectPlaza, PublisherOrdersString);
                }
                if (_publisher.State == State.Closed)
                {
                    _publisher.Open();
                }
                if (_publisher.State == State.Error)
                {
                    try
                    {
                        _publisher.Close();
                        _publisher.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }

                    _publisher = null;
                }
            }
            catch (Exception error)
            {
                try
                {
                    _publisher.Dispose();
                }
                catch
                {
                    // ignore
                }

                _publisher = null;

                SendLogMessage(error);
            }

            try
            {
                if (_publisher == null || _publisher.State != State.Active)
                {
                    return;
                }
                if (_listenerOrderSendMirror == null)
                {
                    _listenerOrderSendMirror = new Listener(ConnectionObjectPlaza, ListnerOrders);
                    _listenerOrderSendMirror.Handler += ClientMessage_OrderSenderMirror;
                    return;
                }
                if (_listenerOrderSendMirrorNeadToReload || _listenerOrderSendMirror.State == State.Error)
                {
                    _listenerOrderSendMirrorNeadToReload = false;
                    try
                    {
                        _listenerOrderSendMirror.Close();
                        _listenerOrderSendMirror.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }

                    _listenerOrderSendMirror = null;
                    return;
                }
                if (_listenerOrderSendMirror.State == State.Closed)
                {
                    if (_listenerOrderSenderMirrorReplState == null)
                    {
                        _listenerOrderSendMirror.Open("mode=online");
                    }
                    else
                    {
                        _listenerOrderSendMirror.Open("mode=snapshot+online;" + "replstate=" + _listenerOrderSenderMirrorReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerOrderSendMirror.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerOrderSendMirror = null;
                SendLogMessage(error);
            }
        }

        /// <summary>
		/// here my trade table and order log are processed
        /// здесь обрабатывается таблица мои сделки и ордерЛог
        /// </summary>
        public int ClientMessage_OrderSenderMirror(
            Connection conn, Listener listener, Message msg)
        {

            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgData:
                        {
                            try
                            {
                                DataMessage msgData = (DataMessage)msg;
                                RouterLogger.Log($"{msgData.MsgId}; {msgData.ToString()}", "OrderSenderMirror");
                                if (msgData.MsgId == 99)
                                {
                                    SendLogMessage("Слишком много заявок отправлено. Флуд" + msgData);
                                    int NumberUser = Convert.ToInt32(msgData.UserId);
                                    string msgCode = msgData["message"].asUnicodeString();
                                    if (Orders.TryGetValue(NumberUser, out var order))
                                    {
                                        if (order.state == Order.OrderStateType.Pending)
                                        {
                                            order.state = Order.OrderStateType.Fail;
                                            NewCanceledOrder?.Invoke(order, msgCode);
                                        }
                                    }

                                    return 0;
                                }
                                else if (msgData.MsgId == 100)
                                {
                                    SendLogMessage(msgData.ToString());
                                    ReconnectPlaza();
                                    return 0;
                                }
                                else if (msgData.MsgId == 179)  
                                {
                                    var timeSet = PLazaHelper.GetTimeMoscowNow();
                                    int code = msgData["code"].asInt();
                                    string NumberMarket = msgData["order_id"].asUnicodeString();
                                    int NumberUser = Convert.ToInt32(msgData.UserId);
                                   
                                    if (!Orders.TryGetValue(NumberUser, out var order))
                                    {
                                        order = new Order();
                                        order.numberUser = NumberUser;
                                    }
                                    if (NumberMarket.Length > 1)
                                    {
                                        order.SetNumberMarket(NumberMarket);
                                    }
                                    order.timeSet = timeSet;
                                    Orders[order.NumberUserOrderId] = order;

                                    string msgCode = msgData["message"].asUnicodeString();


                                    if (code == 0)
                                    {
                                        if (order.state == Order.OrderStateType.Pending)
                                        {
                                            order.state = Order.OrderStateType.Activ;
                                        }
                                        NewActiveOrder?.Invoke(order);
                                    }
                                    else
                                    {
                                        
                                        order.state = Order.OrderStateType.Fail;
                                        NewCanceledOrder?.Invoke(order, msgCode);
                                    }

                                    OrderChangedEvent?.Invoke(order, msgCode);
                                }
                                else if (msgData.MsgId == 186)
                                {
                                    int numberUser = Convert.ToInt32(msgData.UserId);
                                    Orders.TryGetValue(numberUser, out var order);

                                    int code = msgData["code"].asInt();
                                    string message = msgData["message"].asUnicodeString();
                                    int num_orders = msgData["num_orders"].asInt();
                                    var oldState = order?.state;
                                    if (code == 0)
                                    {
                                        if (order != null && order?.state != Order.OrderStateType.Done)
                                        {
                                            order.state = Order.OrderStateType.Cancel;
                                        }
                                    }
                                    if (oldState != order?.state)
                                    {
                                        OrderChangedEvent?.Invoke(order, $"{message} Обработано: {num_orders}");
                                    }
                                }

                            }
                            catch (Exception error)
                            {
                                SendLogMessage(error);
                            }

                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerOrderSendMirrorNeadToReload = true;
                            _listenerOrderSenderMirrorReplState = ((P2ReplStateMessage)msg).ReplState;
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
        }

        /// <summary>
		/// execute order
        /// исполнить ордер
        /// </summary>
        private void ExecuteOrder2(Order order)
        {
            _ordersToExecute.Enqueue(order);
            Orders[order.NumberUserOrderId] = order;
        }


        int Lastsecond = -1;

        //супер простой лок надо поставить
        int limit = 0;

        /// <summary>
        /// Выставление ордера на биржу
        /// Ответ в Event NewActiveOrder(order);
        /// </summary>
        /// <param name="order"></param>
        public async Task<string> ExecuteOrderAsync(Order order)
        {
            RouterLogger.Log($"ExecuteOrder {order} start" , "ExecuteOrder");
            if (order.TypeOrder == Order.OrderType.Market)
            {
                order.priceOrder = order.Side == Side.Buy ? Securities[order.SecurityId].PriceLimitHigh : Securities[order.SecurityId].PriceLimitLow;
            }

            Orders[order.NumberUserOrderId] = order;

            if (Emulation)
            {

                order.ExchangeOrderId = DateTime.Now.GetHashCode().ToString();
                order.state = Order.OrderStateType.Activ;

                OrderChangedEvent?.Invoke(order, "The order has been sent.");

                var timer = new System.Timers.Timer(30000) { AutoReset = false };
                timer.Elapsed += (s, e) =>
                {
                    order.state = Order.OrderStateType.Done;
                    OrderChangedEvent?.Invoke(order, "The order has been executed.");
                };
                timer.Start();

                return $"Request for Emulation order #{order.numberUser} sent.";
            }


            Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "AddOrder");
            DataMessage smsg = (DataMessage)sendMessage;

            int dir = order.Side == Side.Buy ? 1 : 2;

            string code = order.PortfolioNumber;
            string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() +
                                    code[3].ToString();
            string clientCode = code[4].ToString() + code[5].ToString() + code[6].ToString();

           
            smsg.UserId = (uint)order.NumberUserOrderId;
            smsg["broker_code"].set(brockerCode);

            int isinId = Convert.ToInt32(order.SecurityId);

            smsg["isin_id"].set(isinId);
            smsg["client_code"].set(clientCode);
            smsg["type"].set(1);
            smsg["dir"].set(dir);
            smsg["amount"].set(Convert.ToInt32(order.Volume));
            smsg["price"].set(order.priceOrder.ToString(new CultureInfo("en-US")));
            smsg["ext_id"].set(order.NumberUserOrderId);
            smsg["comment"].set(order.Comment);
            order.timeCreate = GetTimeMoscowNow();

           

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    rateGateOrderWork.WaitToProceed();

                    try
                    {
                        _publisher.Post(sendMessage, PublishFlag.NeedReply);
                    }
                    catch
                    {
                        order.state = Order.OrderStateType.Fail;
                    }

                    NewActiveOrder?.Invoke(order);
                    sendMessage.Dispose();

                    RouterLogger.Log($"Order {order} has been  sent.", "ExecuteOrder");
                    OrderChangedEvent?.Invoke(order, "The order has been sent.");

                   
                }
                catch (Exception ex)
                {
                    order.state = Order.OrderStateType.Fail;
                    NewActiveOrder?.Invoke(order);
                    
                    OrderChangedEvent?.Invoke(order, "Error sending order.");

                    SendLogMessage(ex);
                }
            });
            
            
            

            RouterLogger.Log($"Request for placing order #{order.numberUser} sent.", "ExecuteOrder");
            return $"Request for placing order #{order.numberUser} sent.";
        }

        /// <summary>
        /// Тариф по числу транзакций в секунду (минимальный 30/сек)
        /// </summary>
        public int Limit { get; set; }

        object limitcounter = new object();
        private void IncreaseLimit()
        {
            lock (limitcounter)
            {
                var datetimesecondNow = DateTime.Now.Second;

                if (Lastsecond == datetimesecondNow)
                    limit++;
                else
                {
                    Lastsecond = datetimesecondNow;
                    limit = 0;
                    limit++;
                }

                SendLogMessage($"Current Limit in sec =  {limit}");
            }
        }

        public bool CanPlaceOrders { get {
                {
                    lock (limitcounter)
                    {
                        var datetimesecondNow = DateTime.Now.Second;
                        
                        if (Lastsecond != datetimesecondNow)
                        { 
                            limit = 0;
                            return true;
                        }

                        if (limit >= Limit)
                            return false;
                        else return true;
                    }
                }
            } }

        /// <summary>
        /// Отозвать ордер из системы по его номеру
        /// </summary>
        /// <param name="numberUser"></param>
        public async Task<bool> CancelOrder(int numberUser)
        {
            RouterLogger.Log($"Start Cancel Order {numberUser}", "CancelOrder");

            if (numberUser <= 0)
            {
                RouterLogger.Log($"Error: The order number {numberUser} is incorrect.", "CancelOrder");
                return false;// $"Error: The order number {numberUser} is incorrect.";
            }

            if (!Orders.TryGetValue(numberUser, out Order order) )
            {
                RouterLogger.Log($"Error: The order number {numberUser} not found.", "CancelOrder");
                return false;
            }
            RouterLogger.Log($"Canceling Order {order?.ToString()??"NULL"}", "CancelOrder");

            if(Emulation)
            {
                var timer = new System.Timers.Timer(100) { AutoReset = false };
                timer.Elapsed += (s, e) =>
                {
                    order.state = Order.OrderStateType.Cancel;
                    OrderChangedEvent?.Invoke(order, "The order has been cancelled.");
                };
                timer.Start();

                return true;
            }

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    rateGateOrderWork.WaitToProceed();

                    Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "DelUserOrders");
                    string code = order?.PortfolioNumber??"";
                    if (code.Length == 0)
                    {
                        code = Portfolios?.First().Value.Number;
                    }
                    if ((code?.Length ?? 0) < 7)
                    {
                        RouterLogger.Log($"Error cancel Order #{numberUser}. Portfolio not found.", "CancelOrder");
                    }
                    
                    string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() +
                    code[3].ToString();
                    string clientCode = code[4].ToString() + code[5].ToString() + code[6].ToString();

                    DataMessage smsg = (DataMessage)sendMessage;

                    smsg.UserId = (uint)numberUser;

                    smsg["broker_code"].set(brockerCode);   // обязательные поля
                    smsg["code"].set(clientCode);
                    
                    smsg["buy_sell"].set(3);    // любое направление
                    smsg["non_system"].set(2);    // все заявки
                    smsg["base_contract_code"].set("%");  //любой контракт
                    smsg["isin_id"].set(0); // любой инструмент
                    smsg["instrument_mask"].set(7); // по всем
                    smsg["ext_id"].set(numberUser);

                    _publisher.Post(sendMessage, PublishFlag.NeedReply);

                    sendMessage.Dispose();
                    
                }
                catch (Exception ex)
                {
                    RouterLogger.Log($"Cancel Order {order} error {ex.Message}", "CancelOrder");
                    SendLogMessage(ex);
                }
            });
            RouterLogger.Log($"Request to cancel order {numberUser} sent.", "CancelOrder");
            return true;// $"Request to cancel order {numberUser} sent.";
            
        }


        /// <summary>
		/// cancel order from the system
        /// отозвать ордер из системы
        /// </summary>
        private void CancelOrderOld(Order order)
        {
            if (_canseledOrders == null)
            {
                _canseledOrders = new List<decimal[]>();
            }

            decimal[] record = _canseledOrders.Find(decimals => decimals[0] == order.NumberUserOrderId);

            if (record != null)
            {
                record[1] += 1;
				// if algorithm tries to remove one bid five times, then order cancellation will be ignored
                // если алгоритм пытается снять одну заявку пять раз, то снятие этой заявки будет проигнорировано
                if (record[1] > 5)
                {
                    return;
                }
            }
            else
            {
                record = new[] { Convert.ToDecimal(order.NumberUserOrderId), 1 };
                _canseledOrders.Add(record);
            }

            _ordersToCansel.Enqueue(order);
        }

        public void CancelOrderByExchangeId(Order order)
        {
            try
            {
                IncreaseLimit();

                if (order == null ) return;
                if (order.ExchangeOrderId == "") return;

                Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "DelOrder");

                string code = order.PortfolioNumber;

                string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() +
                                     code[3].ToString();

                DataMessage smsg = (DataMessage)sendMessage;
                smsg.UserId = (uint)order.NumberUserOrderId;
                smsg["broker_code"].set(brockerCode);
                smsg["order_id"].set(Convert.ToInt64(order.ExchangeOrderId));

                _publisher.Post(sendMessage, PublishFlag.NeedReply);
                sendMessage.Dispose();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex);
            }
        }

        /// <summary>
		/// thread standing on the order queue and monitoring the execution of trades
        /// поток стоящий на очереди заявок и следящий за исполнением сделок
        /// </summary>
        private Thread _threadOrderExecutor;

        /// <summary>
		/// number of orders in the last second
        /// количество заявок в последней секунде
        /// </summary>
        private int _countActionInThisSecond = 0;

        /// <summary>
		/// last second in which we placed orders
        /// последняя секунда, в которой мы выставляли заявки
        /// </summary>
        private DateTime _thisSecond = DateTime.MinValue;

        /// <summary>
		/// method where the stream is running sending orders to the system
        /// метод где работает поток высылающий заявки в систему
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        private void ExecutorOrdersThreadArea()
        {
            while (Thread.CurrentThread.Name != "deleteThread")
            {
                if (Status == ServerConnectStatus.Connect && _ordersToExecute != null && _ordersToExecute.Count != 0)
                {
                    if (_thisSecond != DateTime.MinValue &&
                        _thisSecond.AddSeconds(1) > GetTimeMoscowNow() &&
                        _countActionInThisSecond >= MaxTransaction - 1)
                    {
                        continue;
                    }

                    if (_thisSecond.AddSeconds(1) < GetTimeMoscowNow() ||
                        _thisSecond == DateTime.MinValue)
                    {
                        _thisSecond = GetTimeMoscowNow();
                        _countActionInThisSecond = 0;
                    }
                    try
                    {
                        Order order = _ordersToExecute.Dequeue();

                        //lock (_plazaThreadLocker)
                        {
                            Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "AddOrder");

                            int dir = 0;

                            if (order.Side == Side.Buy)
                            {
                                dir = 1;
                            }
                            else
                            {
                                dir = 2;
                            }

                            string code = order.PortfolioNumber;

                            string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() +
                                                 code[3].ToString();
                            string clientCode = code[4].ToString() + code[5].ToString() + code[6].ToString();

                            DataMessage smsg = (DataMessage)sendMessage;
                            smsg.UserId = (uint)order.NumberUserOrderId;
                            smsg["broker_code"].set(brockerCode);

                            int isinId =/* GetIsinId(*/ Convert.ToInt32(order.SecurityId);//);

                           /* if(isinId == -1)
                            {
                                continue;
                            }*/

                            smsg["isin_id"].set(isinId);
                            smsg["client_code"].set(clientCode);
                            smsg["type"].set(1);
                            smsg["dir"].set(dir);
                            smsg["amount"].set(Convert.ToInt32(order.Volume));
                            smsg["price"].set(order.PriceOrder.ToString(new CultureInfo("en-US")));
                            smsg["ext_id"].set(order.NumberUserOrderId); 

                            smsg["comment"].set(order.Comment);
                            //Debug.WriteLine(sendMessage);

                            _publisher.Post(sendMessage, PublishFlag.NeedReply);
                            sendMessage.Dispose();
                            var time = GetTimeMoscowNow();
                            SendLogMessage("выслали ордер из коннектора " + time.ToString() + ":" + time.Millisecond);
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }
                }
                else if (Status == ServerConnectStatus.Connect && _ordersToCansel != null && _ordersToCansel.Count != 0)
                {
                    if (_thisSecond != DateTime.MinValue &&
                        _thisSecond.AddSeconds(1) > GetTimeMoscowNow() &&
                        _countActionInThisSecond >= MaxTransaction - 1)
                    {
                        continue;
                    }

                    if (_thisSecond.AddSeconds(1) < GetTimeMoscowNow() ||
                        _thisSecond == DateTime.MinValue)
                    {
                        _thisSecond = GetTimeMoscowNow();
                        _countActionInThisSecond = 0;
                    }

                    try
                    {
                        Order order = _ordersToCansel.Dequeue();
                        //lock (_plazaThreadLocker)
                        {
                            Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "DelOrder");

                            string code = order.PortfolioNumber;

                            string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() +
                                                 code[3].ToString();

                            DataMessage smsg = (DataMessage)sendMessage;
                            smsg.UserId = (uint)order.NumberUserOrderId;
                            smsg["broker_code"].set(brockerCode);
                            smsg["order_id"].set(Convert.ToInt64(order.ExchangeOrderId));

                            _publisher.Post(sendMessage, PublishFlag.NeedReply);
                            sendMessage.Dispose();
                            _countActionInThisSecond++;
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        //private int GetIsinId(string isin)
        //{

        //    for (int i = 0; i < _securities.Count; i++)
        //    {
        //        if(_securities[i].Name.Equals(isin))
        //        {
        //            return Convert.ToInt32(_securities[i].ID);

        //        }
        //    }

        //    return -1;
        //}

        /// <summary>
		/// order queue for execution
        /// очередь заявок на исполнение
        /// </summary>
        private Queue<Order> _ordersToExecute;

        /// <summary>
		/// order queue for cancellation
        /// очередь заявок на отмену
        /// </summary>
        private Queue<Order> _ordersToCansel;

        private List<decimal[]> _canseledOrders;

        // CODHeartbeat

        /// <summary>
        /// CODHeartbeat thread / поток отправляющий CODHeartbeat
        /// COD - specially ordered service from a broker/COD - специально заказываемая услуга у брокера
        /// allows you to delete all active orders of your login when the connection with your program is broken/позволяет при обрыве связи с Вашей программой удалять все активные заявки ВАшего логина
        /// It works as well - while signals from the program are being sent - everything is ok. How to stop - the exchange cancels all orders./Работает так- пока сигналы из программы идут - всё ок. Как прекращаются - биржа кроет все заявки.
        /// </summary>
        private Thread _heartBeatSenderThread;

        /// <summary>
		/// work place of thread sending signals CODHeartbeat to the system
        /// место работы потока отправляющего сигналы CODHeartbeat в систему
        /// </summary>
        private void HeartBeatSender()
        {
            while (Thread.CurrentThread.Name != "deleteThread")
            {
                Thread.Sleep(5000);

                //lock (_plazaThreadLocker)
                {
                    try
                    {
                        if (_publisher != null && _publisher.State == State.Active)
                        {
                            Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "CODHeartbeat");
                            _publisher.Post(sendMessage, 0);

                            sendMessage.Dispose();
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error);
                    }
                }
            }
        }
	
		// sending messages to up
        // Отправка сообщений на верх

        /// <summary>
		/// send log message
        /// отправить сообщение в лог
        /// </summary>
        public void SendLogMessage(string message)
        {
            LogMessageEvent?.Invoke(message);
            RouterLogger.Log(message, "Exeption");
            
        }


        public void SendLogMessage(Exception exception, [CallerMemberName] string MemberName = "",
                                                [CallerFilePath] string sourceFilePath = "",
                                                [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                if (exception is ThreadAbortException)
                {
                    return;
                }
                if (exception is AggregateException)
                {
                    AggregateException httpError = (AggregateException)exception;

                    for (int i = 0; i < httpError.InnerExceptions.Count; i++)

                    {
                        Exception item = httpError.InnerExceptions[i];

                        if (item is NullReferenceException == false)
                        {
                            if (item.InnerException == null)
                            {
                                SendLogMessage(exception.ToString());

                            }
                            else
                            {
                                SendLogMessage(sourceFilePath + "; "+ MemberName + "; " + sourceLineNumber.ToString() +";\n    " + item.InnerException.Message + $" {exception.StackTrace}");
                            }
                        }

                    }
                }
                else
                {
                    SendLogMessage(sourceFilePath + "; " + MemberName + "; " + sourceLineNumber.ToString() + ";\n    " + exception.Message + $" {exception.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(exception.ToString());
                SendLogMessage(ex.ToString());
            }
        }

        /// <summary>
		/// called when a new log message appears
        /// вызывается когда появлось новое сообщение в Лог
        /// </summary>
        public event Action<string> LogMessageEvent;
    }
}
