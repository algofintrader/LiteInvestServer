using Microsoft.VisualBasic;
using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

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
        /// Возвращает среднее значение входа. 
        /// </summary>
        /// <param name="trade"></param>
        /// <returns>СРЕДНЮЮ ЦЕНУ</returns>
        public AggregationTrade AddTrade(MyTrade trade)
        {
            Trades.TryAdd(trade.NumberTrade, trade);
            var result = CalculateAveragePrice(Trades.Values);
            
            AveragePrice = result.AveragePrice;
            Volume = result.AllVol;

            return result;
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


    /// <summary>
    /// По сути позиция с открытием, закрытием. 
    /// </summary>
    [DataContract]
    public class Pos
    {

        TradeCollection OpenTrades = new();
        TradeCollection CloseTrades = new();

        [JsonIgnore]
        [DataMember]
        public decimal CurrentPos { get; private set; } = 0;

        [DataMember]
        public decimal PosValue { get => Side == Side.Buy? CurrentPos : - CurrentPos; }

        [DataMember]
        public Side Side { get; private set; }

        [DataMember]
        public string StringSide { get => Side.ToString(); }

        [DataMember]
        public decimal AverageEntry{ get; private set; }
        [DataMember]
        public decimal AverageExit { get; private set; }

        [DataMember]
        public bool Open { get; private set; }

        [DataMember]
        public decimal MaxOpened { get; set; }

        [DataMember]
        public decimal RealizedPnl { get; private set; }
        [DataMember]
        public decimal UnRealizedPnl { get; private set; }

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

                UnRealizedPnl = (closedtrade.AveragePrice - OpenTrades.AveragePrice) * posopened;

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

        private void UpdateOpen(AggregationTrade aggregationTrade)
        {
            MaxOpened = aggregationTrade.AllVol;
            AverageEntry = aggregationTrade.AveragePrice;
        }

        public Pos? AddTrade(MyTrade myTrade)
        {
           

            //первая сделка открывает направление
            if (CurrentPos == 0)
            {
                Side = myTrade.Side;
                CurrentPos += myTrade.Volume;

                Open = true;

                var res= OpenTrades.AddTrade(myTrade);
                UpdateOpen(res);

                return null;
            }

            //пришло обратное направление (то есть чел, перезакрывается)
            if (CurrentPos != 0 && Side != myTrade.Side)
            {
                //если перезакрытие уже больше
                if (myTrade.Volume >= CurrentPos)
                {
                    Open = false;
                    var rest = Math.Abs(CurrentPos) - Math.Abs(myTrade.Volume);

                    CurrentPos = 0;

                    //поза закрыта и остатка нет
                    if (rest == 0)
                    {
                        AverageExit = CloseTrades.AddTrade(myTrade).AveragePrice;
                        RealizedPnl = CalculateFinishedPnl();
                        return null;
                    }
                    //поза закрыта и есть остаток
                    else
                    {
                        var pos = new Pos();
                        pos.AddTrade(new MyTrade() { Side = myTrade.Side, Price = myTrade.Price,Volume= myTrade.Volume });
                        return pos;
                    }

                    return null;
                }


                if (myTrade.Volume < CurrentPos)
                {
                    CurrentPos -= myTrade.Volume;
                    AverageExit = CloseTrades.AddTrade(myTrade).AveragePrice;
                    return null;

                }

                return null;
            }
           
            if(CurrentPos !=0 && Side == myTrade.Side)
            {
               
                CurrentPos += myTrade.Volume;
                var res = OpenTrades.AddTrade(myTrade);
                UpdateOpen(res);

                return null;
            }

            return null;

            //как мы будем закрывать открывать позицию и где будет эта логика?
        }

     
    }
}
