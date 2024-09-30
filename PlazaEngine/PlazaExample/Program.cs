using PlazaEngine.Engine;
using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

class Program
{
    private static PlazaConnector plazaConnector;
    static ConcurrentDictionary<string, Security> ListOfSecuritiesWithId = new ConcurrentDictionary<string, Security>();
    static ConcurrentDictionary<string, Security> SecuritiesFullName = new ConcurrentDictionary<string, Security>();
    static List<Security> Securities = new List<Security>();

    static Portfolio Portfolio;
    private static Security security;

    /// <summary>
    /// Инструмент для теста
    /// </summary>
    private static string mainInstrument = "BRU4/BR-9.24@FORTS";
    private static bool showticks;
    private int marketupdate;

    static void Main(string[] args)
    {
        //русский язык для консоли
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("Start Plaza App");

        //try
        {

            //тестовый ключ. Для боевого нужен будет лицензионный ключ 
            plazaConnector = new PlazaConnector("11111111", true)
            {
                //лимит по ордера 
                Limit = 30,
                //надо ли грузить историю тиков
                LoadTicksFromStart = false,
            };

            plazaConnector.LogMessageEvent += LogMessage;
            plazaConnector.ConnectStatusChangeEvent += PlazaControllerOnConnectStatusChangeEvent;

            plazaConnector.UpdatePortfolio += PlazaControllerOnUpdatePortfolio;
            plazaConnector.UpdatePosition += PlazaControllerOnUpdatePosition;
            plazaConnector.UpdateSecurity += PlazaControllerOnUpdateSecurity;

            plazaConnector.NewTickEvent += PlazaControllerOnNewTickEvent;

            plazaConnector.NewMyTradeEvent += trade =>
            {
                var message1 = string.Format("Новая сделка! Время {0} НомерОрдера {1} Номер сделки {2}", trade.Time, trade.NumberOrderParent, trade.NumberTrade);
                LogMessage(message1);
            };

            plazaConnector.NewCanceledOrder += order1 =>
            {
                //if (order1.NumberUser == order.NumberUser)
                LogMessage("Наш ордер отменился " + order1.NumberUser+ " comment = " + order1.Comment);
            };

            plazaConnector.NewActiveOrder += order1 =>
            {
                //if (order1.NumberUser == order.NumberUser)
                LogMessage("Наш ордер выставился " + order1.NumberUser + " comment = " + order1.Comment);
            };

            plazaConnector.NewOrderFilled += neworder =>
            {
                //if (neworder.NumberUser == order.NumberUser)
                LogMessage("Наш ордер выполнился " + neworder.NumberUser + " comment = " + neworder.Comment);
            };

            plazaConnector.Connect();

            WaitConsole();

        }
        //catch (Exception ex)
        {
          //  LogMessage(ex.Message);
        }

    }

    private static void WaitConsole()
    {
        var letter = Console.ReadLine();



        if (letter == "e")
        {
            LogMessage("команда на выход!");
            if (plazaConnector != null)
            {
                plazaConnector.Stop();
                plazaConnector.Dispose();
            }
            return;
        }
        else if(letter == "p")
        {
            LogMessage("команда на регистрацию/отмену стакана!");
            StartStopMarketDepth(mainInstrument);

            WaitConsole();
        }
        else if (letter == "o")
        {
            LogMessage("команда на размещение ордера!");
            PlaceOrder();

            WaitConsole();
        }
        else
        {
            LogMessage("Нет такой команды");
            WaitConsole();
        }
    }

    public static int GetDecimalDigitsCount(decimal number)
    {
        string[] str = number.ToString(new System.Globalization.NumberFormatInfo() { NumberDecimalSeparator = "." }).Split('.');
        return str.Length == 2 ? str[1].Length : 0;
    }
    private static void PlaceOrder()
    {

        // BRU4/BR-9.24 цена 171,21000
        
        if (plazaConnector.Status != ServerConnectStatus.Connect)
            plazaConnector.Status = ServerConnectStatus.Connect;

        security = SecuritiesFullName[mainInstrument];

        if (security == null)
        {
            LogMessage("инструмент найден");
            return;
        }

        var amount = GetDecimalDigitsCount(security.PriceStep);

        var order = new Order()
        {
            TypeOrder = Order.OrderPriceType.Limit,
            Price = Math.Round(171.20m, amount),
            SecurityNameCode = security.MainId,
            SecurityClassCode = security.ClassCode,
            Volume = 1,
            Side = Side.Buy,
            PortfolioNumber = Portfolio.Number,
            NumberUser = Helper.CreateHashId(),
        };

        order.Comment = "clientID " + Helper.CreateHashId();

        var message = string.Format($"Выставляю ордер цена {order.Price} направление = {order.Side} клиентский код = {order.NumberUser} коммент {order.Comment}"); 
        LogMessage(message);

        plazaConnector.ExecuteOrder(order);
       
    }

    private static void StartStopMarketDepth(string longcode)
    {
        try
        {
            if (security != null)
            {
                plazaConnector.StopMarketDepth(security);
                security.DepthUpdated -= Security_DepthUpdated;

                security = null;
                return;
            }
        }
        catch (Exception ex)
        {

        }

        Levels.Clear();
        security = SecuritiesFullName[longcode];

        if (security == null)
        {
            LogMessage("Инструмент не найден на подписку на стакан!");
            return;
        }

        security.DepthUpdated += Security_DepthUpdated;

        plazaConnector.RegisterMarketDepth(security);

        LogMessage("Подписались на стакан " + longcode);
    }
    static List<Level> Levels = new List<Level>();
    private static void Security_DepthUpdated(MarketDepth marketdepth)
    {
        if (marketdepth != null)
        {
            Levels.Clear();

            if (marketdepth.Asks != null)
            {
                foreach (var item in marketdepth.Asks.ToList())
                {
                    Levels.Add(new Level() { Price = item.Price, Quantity = item.Ask, Direction = "Sell" });
                }
            }
            if (marketdepth.Bids != null)
            {
                foreach (var item in marketdepth.Bids)
                {
                    Levels.Add(new Level() { Price = item.Price, Quantity = item.Bid, Direction = "Buy" });
                }
            }

            if (marketdepth.Asks != null && marketdepth.Bids != null)
                LogMessage(marketdepth.SecurityNameCode + "BIDS=" +  marketdepth.Bids.Count() + "ASKS="  + marketdepth.Asks.Count());
        } 

    }


    /// <summary>
    /// Сюда поступают все тики с самого старта дня либо с момента запуска (зависит от настройки)
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="arg2"></param>
    private static void PlazaControllerOnNewTickEvent(Trade trade, bool arg2)
    {
        if (ListOfSecuritiesWithId.ContainsKey(trade.SecurityId) /*&& showticks*/)
        {
            var security = ListOfSecuritiesWithId[trade.SecurityId];
            var code = ListOfSecuritiesWithId[trade.SecurityId].ShortName;

            //LogMessage($"новый тик {code}/{security.Name} цена {trade.Price}");
        }
    }

  

    static private void PlazaControllerOnConnectStatusChangeEvent(ServerConnectStatus obj)
    {
        Debug.WriteLine("Статус изменился " + obj);
    }
    private static void PlazaControllerOnUpdateSecurity(Security security)
    {
        ListOfSecuritiesWithId[security.MainId] = security;
      

        var name = security.ShortName + "/" + security.Name + "@" + security.ClassCode;
        SecuritiesFullName.TryAdd(name, security);

        Debug.WriteLine("Инструмент ключ" + name + " код ID =  " + security.MainId + " Короткое имя = " + security.ShortName + " LimitUp " + security.PriceLimitHigh + " LimitLow " + security.PriceLimitLow + " Шаг цены " + security.PriceStep);

        // if (!Securities.ContainsKey(security.NameId))
        Securities.Add(security);
    }
    static private void PlazaControllerOnUpdatePortfolio(Portfolio portfolio)
    {
        Portfolio = portfolio;
        LogMessage(string.Format("Портфель {0}", portfolio.ValueCurrent));
    }
    static private void PlazaControllerOnUpdatePosition(PositionOnBoard pos)
    {
        LogMessage(string.Format("Позиция {0} номер инструмента {1} ", pos.ValueCurrent, pos.SecurityNameCode));
    }

    static void LogMessage(string message)
    {
        Console.WriteLine(message);
    }
}