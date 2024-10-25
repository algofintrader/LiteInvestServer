using Microsoft.AspNetCore.Connections;
using Microsoft.VisualBasic;
using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Transactions;

namespace LiteInvestServer.Entity
{
 
 /*
 цена фьючерса = 90 000 р. 
 Лимит = 30 000 р. 

 Плечо получается = цена / лимит 
 плечо = 3 

 проверить плечо выданное к плечу которое есть. 
 свободные финансы < */
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


    public class TradeAddResult
    {
        public bool Closed { get; set; }
        //public Pos NewPos { get; set; }

        public MyTrade RestNewTrade { get; set; }
    }

    /// <summary>
    /// По сути позиция с открытием, закрытием. 
    /// </summary>
    [DataContract]
    public class Pos
    {
        private decimal _comission = 0.02m / 100;

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
        public DateTime OpenTime { get; private set; }

        [DataMember]
        public DateTime CloseTime { get; private set; }

        [DataMember]
        public decimal AverageExit { get; private set; }

        [DataMember]
        public bool Open { get; private set; }

        [DataMember]
        public decimal MaxOpened { get; set; }

        [DataMember]
        public decimal Comission => MaxOpened * 2 * _comission;

        /// <summary>
        /// сразу считает 
        /// </summary>
        [DataMember]
        public decimal RealizedPnl { get; private set; }

        /// <summary>
        /// Прибыль в пунтках
        /// </summary>
        [DataMember]
        public decimal RealizedPnlPoints { get; private set; }


        [DataMember]
        public decimal UnRealizedPnl { get; private set; }

        [DataMember]
        public decimal UnRealizedPnlPoints { get; private set; }

        object tradelock = new object();

        private int multDirection => Side == Side.Buy ? 1 : -1;

        public decimal SimulationExit { get; set; }

        public void CalculateUnrealizedPnl(decimal tickprice)
        {

            //как бы если поза не открыта, мы уже ниче не делаем.
            if (CurrentPos == 0 || !Open)
                return;

            lock(tradelock)
            {
                SimulationExit = tickprice;

                var posopened = OpenTrades.Volume;
                var posclosed = CloseTrades.Volume;

                var rest = posopened - posclosed;

                var closedtrade = CloseTrades.SimulateTradeCalculations(new MyTrade()
                {
                    Price = tickprice,
                    Volume = rest
                });

                UnRealizedPnlPoints = (closedtrade.AveragePrice - OpenTrades.AveragePrice)* multDirection;
                UnRealizedPnl = UnRealizedPnlPoints * multDirection - Comission;

            }
           
        }

        public void CalculateFinishedPnl()
        {

            lock (tradelock)
            {

                var posopened = OpenTrades.Volume;
                RealizedPnlPoints = (CloseTrades.AveragePrice - OpenTrades.AveragePrice) * multDirection;
                RealizedPnl = RealizedPnlPoints * multDirection * posopened - Comission;

            }

        }

        private void UpdateOpen(AggregationTrade aggregationTrade)
        {
            MaxOpened = aggregationTrade.AllVol;
            AverageEntry = aggregationTrade.AveragePrice;
        }

        public TradeAddResult? AddTrade(MyTrade myTrade)
        {
            try
            {

                //первая сделка открывает направление
                if (CurrentPos == 0)
                {
                    Side = myTrade.Side;
                    CurrentPos += myTrade.Volume;
                    OpenTime = myTrade.Time;
                    Open = true;

                    var res = OpenTrades.AddTrade(myTrade);
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
                        CloseTime = myTrade.Time;

                        AverageExit = CloseTrades.AddTrade(myTrade).AveragePrice;
                        CalculateFinishedPnl();

                        //поза закрыта и остатка нет
                        if (rest == 0)
                        {
                            
                            return new TradeAddResult()
                            {
                                RestNewTrade = null,
                                Closed = true,
                            };
                        }
                        //поза закрыта и есть остаток
                        else
                        {
                            return new TradeAddResult()
                            {
                                RestNewTrade = new MyTrade()
                                {
                                    Side = myTrade.Side,
                                    Price = myTrade.Price,
                                    Volume = Math.Abs(rest),
                                    NumberTrade = myTrade.NumberTrade,
                                    Comment = myTrade.Comment,
                                    SecurityId = myTrade.SecurityId,
                                },

                                Closed = true,
                            };
                        }

                        return null;
                    }


                    if (myTrade.Volume < CurrentPos)
                    {
                        CurrentPos -= myTrade.Volume;
                        CloseTrades.AddTrade(myTrade);
                        return null;

                    }

                    return null;
                }

                if (CurrentPos != 0 && Side == myTrade.Side)
                {

                    CurrentPos += myTrade.Volume;
                    var res = OpenTrades.AddTrade(myTrade);
                    UpdateOpen(res);

                    return null;
                }

                return null;

            }
            catch (Exception ex)
            {
                return null;
            }//как мы будем закрывать открывать позицию и где будет эта логика?
        }

     
    }
}
