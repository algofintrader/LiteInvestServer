using System.Globalization;
using System.Text;

namespace LiteInvest.Entity.PlazaEntity
{
    /// <summary>
    /// customer transaction on the exchange
    /// Клиентская сделка, совершённая на бирже
    /// </summary>
    public class MyTrade : IMyTrade
    {



        /// <summary>
        /// Клиентская сделка, совершённая на бирже
        /// </summary>
        /// <param name="replmsg"></param>
        //internal MyTrade(StreamDataMessage replmsg)
        //{
        //    price = Convert.ToDecimal(replmsg["price"].asDecimal());
        //    numberTrade = replmsg["id_deal"].asUnicodeString();
        //    securityId = replmsg["isin_id"].asUnicodeString();
        //    time = replmsg["moment"].asDateTime();
        //    volume = replmsg["xamount"].asInt();

        //    string portfolioBuy = replmsg["code_buy"].asUnicodeString();
        //    string portfolioSell = replmsg["code_sell"].asUnicodeString();
        //    comment = "";
        //    numberOrderMarket = "";
        //    portfolio = "";

        //    if (!string.IsNullOrWhiteSpace(portfolioBuy))
        //    {
        //        numberOrderUser = replmsg["ext_id_buy"].asInt();
        //        numberOrderMarket = replmsg["public_order_id_buy"].asUnicodeString();
        //        side = Side.Buy;
        //        comment = replmsg["comment_buy"].asUnicodeString();
        //        portfolio = replmsg["code_buy"].asUnicodeString();
        //    }
        //    else if (!string.IsNullOrWhiteSpace(portfolioSell))
        //    {

        //        numberOrderUser = replmsg["ext_id_sell"].asInt();
        //        numberOrderMarket = replmsg["public_order_id_sell"].asUnicodeString();
        //        side = Side.Sell;
        //        comment = replmsg["comment_sell"].asUnicodeString();
        //        portfolio = replmsg["code_sell"].asUnicodeString();
        //    }

        //}
        public MyTrade()
        {

        }

        /// <summary>
        /// Комментарий к сделке, перешедший из ордера
        /// </summary>
        public string Comment { get => _comment; set => _comment = value;
        }
        private string _comment;

        /// <summary>
        /// Внутренний номер ордера, по которому прошла сделка
        /// </summary>
        public int NumberOrderUser { get => _numberOrderUser;set => _numberOrderUser = value; }
        private int _numberOrderUser;

        /// <summary>
        /// Код лицевого счета сделки
        /// </summary>
        public string Portfolio { get => _portfolio;
	        set => _portfolio = value;
        }
        private string _portfolio;

        /// <summary>
        /// volume
        /// объём
        /// </summary>
        public decimal Volume { get => _volume; set => _volume = value;
        }
        private decimal _volume;

        /// <summary>
        /// price
        /// цена
        /// </summary>
        public decimal Price { get => _price; set => _price = value;
        }
        private decimal _price;

        /// <summary>
        ///  trade number
        /// номер сделки в торговой системе
        /// </summary>
        public string NumberTrade { get => _numberTrade; set => _numberTrade = value;
        }
        private string _numberTrade;

        /// <summary>
        /// parent's warrant number
        /// номер ордера родителя
        /// </summary>
        public string NumberOrderMarket { get => _numberOrderMarket;
	        set => _numberOrderMarket = value;
        }
        private string _numberOrderMarket;

        /// <summary>
        /// instrument code
        /// код инструмента по которому прошла сделка
        /// </summary>
        public string SecurityId { get => _securityId; set => _securityId = value;
        }
        private string _securityId;

        /// <summary>
        /// time
        /// время
        /// </summary>
        public DateTime Time { get => _time;
	        set => _time = value;
        }
        private DateTime _time;


        /// <summary>
        /// party to the transaction
        /// сторона сделки
        /// </summary>
        public Side Side { get => _side; set => _side = value;
        }
        private Side _side;

        private static readonly CultureInfo CultureInfo = new CultureInfo("ru-RU");

        /// <summary>
        /// to take a line to save
        /// Строка 
        /// </summary>
        public override string ToString()
        {
            return new StringBuilder()
                .Append(Time).Append("; ")
                .Append(SecurityId).Append("; ")
                .Append(Side).Append("; ")
                .Append(Price).Append("; ")
                .Append(Volume).Append("; ")
                .Append(Comment).Append("; ")
                .Append(Portfolio).Append("; ")
                .Append(NumberOrderUser).Append("; ")
                .Append(NumberOrderMarket).Append("; ")
                .Append(NumberTrade).Append("; ")
                .ToString();
        }
    }
}
