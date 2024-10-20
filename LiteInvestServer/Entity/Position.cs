using PlazaEngine.Entity;
using System.Collections.Concurrent;

namespace LiteInvestServer.Entity
{


    /// <summary>
    /// Класс отвечающий за подсчет среднего (вход/выход)
    /// </summary>
    public class TradeCollection
    {

        public decimal Volume { get; private set; }

        /// <summary>
        /// Возвращает значение обще открытых позиций
        /// </summary>
        /// <param name="trade"></param>
        /// <returns></returns>
        public decimal AddTrade(Trade trade)
        {
            Trades.TryAdd(trade.TransactionID, trade);
            return CalculateAveragePrice();
        }

        public decimal AveragePrice { get; set; }

        decimal CalculateAveragePrice()
        {
            decimal averageprice = 0;
            decimal summ = 0;

            foreach (var trade in Trades.Values)
            {
                summ += trade.Volume;
                averageprice += trade.Price * trade.Volume;
            }

            AveragePrice = averageprice / summ;
            return summ;
        }

        ConcurrentDictionary<string, Trade> Trades { get; set; } = new();
    }

    public class Position
    {

        TradeCollection OpenTrades = new();
        TradeCollection CloseTrades = new();

        public decimal CurrentPos { get; private set; } = 0;

        public Side Side { get; private set; }

        public decimal AverageEntry{ get; private set; }
        public decimal AverageExit { get; private set; }

        public bool Open { get; private set; }

        public decimal MaxOpened { get; set; }

        public decimal CalculateUnrealizedPnl(decimal tickprice)
        {




            return 0;
        }

        public void AddTrade(Trade trade)
        {


            //первая сделка открывает направление
            if (CurrentPos == 0)
            {
                Side = trade.Side;
                Open = true;


                CurrentPos = OpenTrades.AddTrade(trade);

                return;
            }

            //пришло обратное направление (то есть чел, перезакрывается)
            if (CurrentPos!=0 && Side!=trade.Side)
            {
                //если перезакрытие уже больше
                if(trade.Volume>CurrentPos)
                {
                    Open = false;
                    var rest = Math.Abs(CurrentPos-trade.Volume);

                    //поза закрыта и остатка нет
                    if(rest==0)
                    {
                        
                    }
                    //поза закрыта и есть остаток
                    else
                    {

                    }

                }

                return;
            }
           
            if(CurrentPos !=0 && Side == trade.Side)
            {
                OpenTrades.AddTrade(trade);

                //TODO: дописать увеличение объёма
                if (OpenTrades.Volume > MaxOpened)
                    MaxOpened = OpenTrades.Volume; 
             
                CurrentPos += trade.Volume;

                return;
            }

            

            //как мы будем закрывать открывать позицию и где будет эта логика?
        }

     
    }
}
