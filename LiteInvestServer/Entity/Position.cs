using Microsoft.VisualBasic;
using PlazaEngine.Entity;
using System.Collections.Concurrent;

namespace LiteInvestServer.Entity
{

    public class AggregationTrade
    {
        public decimal AveragePrice { get; set; }
        public decimal AllVol { get; set; }
    }

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
        public decimal AddTrade(MyTrade trade)
        {
            Trades.TryAdd(trade.NumberTrade, trade);
            var result = CalculateAveragePrice(Trades.Values);
            
            AveragePrice = result.AveragePrice;
            Volume = result.AllVol;

            return Volume;
        }

        public decimal AveragePrice { get; set; }

        /// <summary>
        /// Рассчитывает среднюю цену
        /// КЛЮЧ - весь объём
        /// Value - цена
        /// </summary>
        /// <param name="MyTrades"></param>
        /// <returns></returns>
        AggregationTrade CalculateAveragePrice(ICollection <MyTrade> MyTrades)
        {
            decimal averageprice = 0;
            decimal summ = 0;

            foreach (var trade in MyTrades)
            {
                summ += trade.Volume;
                averageprice += trade.Price * trade.Volume;
            }
           
            return new AggregationTrade() { AllVol = summ, AveragePrice = averageprice / summ };
            // return new AggregationTrade(summ, averageprice / summ);
        }

        public AggregationTrade SimulateTradeCalculations(MyTrade tradeSimulation)
        {
            var clone = Trades.Values.ToList();
            clone.Add(tradeSimulation);

            return CalculateAveragePrice(clone);
        }

        ConcurrentDictionary<string, MyTrade> Trades { get; set; } = new();
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

        object tradelock = new object();

        public decimal CalculateUnrealizedPnl(decimal tickprice)
        {
            
            //как бы если поза не открыта, мы уже ниче не делаем.
            if (CurrentPos == 0 || !Open)
                return 0;

            lock(tradelock)
            {
                var posopened = OpenTrades.Volume;
                var posclosed = CloseTrades.Volume;

                var rest = posopened - posclosed;

                var closedtrade = CloseTrades.SimulateTradeCalculations(new MyTrade()
                {
                    Price = tickprice,
                    Volume = rest
                });

                var pnl = (closedtrade.AveragePrice - OpenTrades.AveragePrice) * posopened;

                return 0;

            }
           
        }

        public decimal CalculateFinishedPnl()
        {

            lock (tradelock)
            {
                var posopened = OpenTrades.Volume;
                return (CloseTrades.AveragePrice - OpenTrades.AveragePrice) * posopened;
            }

        }

        public Position? AddTrade(MyTrade trade)
        {

            //первая сделка открывает направление
            if (CurrentPos == 0)
            {
                Side = trade.Side;
                CurrentPos += trade.Volume;

                Open = true;
                OpenTrades.AddTrade(trade);
                
                return null;
            }

            //пришло обратное направление (то есть чел, перезакрывается)
            if (CurrentPos != 0 && Side != trade.Side)
            {
                //если перезакрытие уже больше
                if (trade.Volume >= CurrentPos)
                {
                    Open = false;
                    var rest = Math.Abs(CurrentPos) - Math.Abs(trade.Volume);

                    //поза закрыта и остатка нет
                    if (rest == 0)
                    {
                        CloseTrades.AddTrade(trade);
                        CalculateFinishedPnl();
                        return null;
                    }
                    //поза закрыта и есть остаток
                    else
                    {
                        var pos = new Position();
                        pos.AddTrade(new MyTrade() { Side = trade.Side, Price = trade.Price,Volume= trade.Volume });
                        return pos;
                    }

                    return null;
                }


                if (trade.Volume < CurrentPos)
                {
                    CloseTrades.AddTrade(trade);

                    return null;

                }

                return null;
            }
           
            if(CurrentPos !=0 && Side == trade.Side)
            {
                OpenTrades.AddTrade(trade);

                //TODO: дописать увеличение объёма
                if (OpenTrades.Volume > MaxOpened)
                    MaxOpened = OpenTrades.Volume; 
             
                CurrentPos += trade.Volume;

                AverageEntry = OpenTrades.AveragePrice;

                return null;
            }

            return null;

            //как мы будем закрывать открывать позицию и где будет эта логика?
        }

     
    }
}
