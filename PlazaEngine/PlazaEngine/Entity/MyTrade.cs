

using ru.micexrts.cgate.message;

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PlazaEngine.Entity
{
    /// <summary>
    /// customer transaction on the exchange
    /// Клиентская сделка, совершённая на бирже
    /// </summary>
    public class MyTrade : IMyTrade
    {

        public MyTrade()
        {

        }

        /// <summary>
        /// Клиентская сделка, совершённая на бирже
        /// </summary>
        /// <param name="replmsg"></param>
        internal MyTrade(StreamDataMessage replmsg)
        {
            price = Convert.ToDecimal(replmsg["price"].asDecimal());
            numberTrade = replmsg["id_deal"].asUnicodeString();
            securityId = replmsg["isin_id"].asUnicodeString();
            time = replmsg["moment"].asDateTime();
            volume = replmsg["xamount"].asInt();

            string portfolioBuy = replmsg["code_buy"].asUnicodeString();
            string portfolioSell = replmsg["code_sell"].asUnicodeString();
            comment = "";
            numberOrderMarket = "";
            portfolio = "";

            if (!string.IsNullOrWhiteSpace(portfolioBuy))
            {
                numberOrderUser = replmsg["ext_id_buy"].asInt();
                numberOrderMarket = replmsg["public_order_id_buy"].asUnicodeString();
                side = Side.Buy;
                comment = replmsg["comment_buy"].asUnicodeString();
                portfolio = replmsg["code_buy"].asUnicodeString();
            }
            else if (!string.IsNullOrWhiteSpace(portfolioSell))
            {

                numberOrderUser = replmsg["ext_id_sell"].asInt();
                numberOrderMarket = replmsg["public_order_id_sell"].asUnicodeString();
                side = Side.Sell;
                comment = replmsg["comment_sell"].asUnicodeString();
                portfolio = replmsg["code_sell"].asUnicodeString();
            }

        }

        /// <summary>
        /// Комментарий к сделке, перешедший из ордера
        /// </summary>
        public string Comment { get => comment; }
        private string comment;

        /// <summary>
        /// Внутренний номер ордера, по которому прошла сделка
        /// </summary>
        public int NumberOrderUser { get => numberOrderUser; }
        private int numberOrderUser;

        /// <summary>
        /// Код лицевого счета сделки
        /// </summary>
        public string Portfolio { get => portfolio; }
        private string portfolio;

        /// <summary>
        /// volume
        /// объём
        /// </summary>
        public decimal Volume { get => volume; set { volume = value; } }
        private decimal volume;

        /// <summary>
        /// price
        /// цена
        /// </summary>
        public decimal Price { get => price; set { price = value; } }
        private decimal price;

        /// <summary>
        ///  trade number
        /// номер сделки в торговой системе
        /// </summary>
        public string NumberTrade { get => numberTrade; }
        private string numberTrade;

        /// <summary>
        /// parent's warrant number
        /// номер ордера родителя
        /// </summary>
        public string NumberOrderMarket { get => numberOrderMarket; }
        private string numberOrderMarket;

        /// <summary>
        /// instrument code
        /// код инструмента по которому прошла сделка
        /// </summary>
        public string SecurityId { get => securityId; }
        private string securityId;

        /// <summary>
        /// time
        /// время
        /// </summary>
        public DateTime Time { get => time; }
        private DateTime time;


        /// <summary>
        /// party to the transaction
        /// сторона сделки
        /// </summary>
        public Side Side { get => side; set { side = value; }  }
        private Side side;

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
