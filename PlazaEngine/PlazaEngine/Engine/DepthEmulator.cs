using PlazaEngine.Entity;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PlazaEngine.Engine
{
    internal class DepthEmulator
    {
        private ConcurrentDictionary<string, Security> _depthEmulators;
        private ConcurrentDictionary<string, Security> _ticksEmulators;
        private Dictionary<string, MarketDepth> _marketDepths;
        private Thread threadEmulating;
        

        public DepthEmulator()
        {
            _depthEmulators = new ConcurrentDictionary<string, Security>();
            _marketDepths = new Dictionary<string, MarketDepth>();
            _ticksEmulators = new ConcurrentDictionary<string, Security>();

            threadEmulating = new Thread(ThreadEmulating);
            threadEmulating.Name = "ThreadEmulating";
            threadEmulating.IsBackground = true;
            //threadEmulating.Start();
        }

        public event Action<MarketDepth>? MarketDepthChanged;
        public event Action<Dictionary<string, List<Trade>>>? NewTickCollectionEvent;


        private void ThreadEmulating()
        {
            int depth = 250;
            try
            {
                Random rnd = new Random(); 
                int counterDeleteLevel = 0;
                while (true)
                {
                    try
                    {
                        Thread.Sleep(200);
                        var security = _depthEmulators.Values.ToList();
                        foreach (var sec in security)
                        {
                            if (!_marketDepths.TryGetValue(sec.Id, out MarketDepth? value))
                            {
                                value = new MarketDepth();
                                _marketDepths[sec.Id] = value;
                            }
                            var md = value;
                            md.SecurityId = sec.Id;
                            md.Time = DateTime.UtcNow.AddHours(3);

                            decimal HiPrice = sec.PriceLimitHigh != 0 ? sec.PriceLimitHigh : 1000;
                            decimal LoPrice = sec.PriceLimitLow != 0 ? sec.PriceLimitLow : 100;
                            if (HiPrice == LoPrice)
                            {
                                HiPrice = HiPrice * 1.1m;
                                LoPrice = LoPrice * 0.9m;
                            }
                            if (md.Asks.Count < 10 || md.Bids.Count < 10)
                            {
                                md.Asks.Clear();
                                md.Bids.Clear();
                                for (int i = 0; i < depth; i++)
                                {
                                    decimal p = Math.Round(HiPrice - (decimal)rnd.NextDouble() * (HiPrice - LoPrice) / 2, sec.Decimals);
                                    int v = rnd.Next(1, 1000);
                                    if (!md.Asks.Exists(a => a.Price == p))
                                    {
                                        md.Asks.Add(new MarketDepthLevel() { Id = i, Price = p, Ask = v });
                                    }

                                    p = Math.Round(LoPrice + (decimal)rnd.NextDouble() * (HiPrice - LoPrice) / 2, sec.Decimals);
                                    v = rnd.Next(1, 1000);
                                    if (!md.Bids.Exists(a => a.Price == p))
                                    {
                                        md.Bids.Add(new MarketDepthLevel() { Id = i, Price = p, Bid = v });
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 0; i <= 5; i++)
                                {
                                    int l = rnd.Next(0, md.Asks.Count - 1);
                                    int v = rnd.Next(1, 1000);
                                    md.Asks[l].Ask = v;
                                    l = rnd.Next(0, md.Bids.Count - 1);
                                    v = rnd.Next(1, 1000);
                                    md.Bids[l].Bid = v;
                                }
                                if (counterDeleteLevel++ > 10)
                                {
                                    counterDeleteLevel = 0;
                                    int ll = rnd.Next(0, md.Asks.Count - 1);
                                    md.Asks.RemoveAt(ll);
                                    ll = rnd.Next(0, md.Bids.Count - 1);
                                    md.Bids.RemoveAt(ll);
                                }
                            }
                            md.Asks.Sort((x, y) => x.Price > y.Price ? -1 : x.Price == y.Price ? 0 : 1);
                            //md.Asks.Sort((x, y) => x.Price > y.Price ? 1 : x.Price == y.Price ? 0 : -1);  // сортировка в другую сторону
                            md.Bids.Sort((x, y) => x.Price > y.Price ? -1 : x.Price == y.Price ? 0 : 1);
                            //md.Bids.Sort((x, y) => x.Price > y.Price ? 1 : x.Price == y.Price ? 0 : -1); // сортировка в другую сторону
                            MarketDepthChanged?.Invoke(md.GetCopy());

                            TickCollectionEmulating(md);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                
            }
        }

        DateTime lastTickCollection = DateTime.Now;

        int MaxTicksRandomPause = 10;   // секунд, чем меньше, тем чаще
        int MaxTicksRandomCountInPack = 10; // максимальное кол-во тиков в одной пачке

        internal void TickCollectionEmulating(MarketDepth md)
        {
            

            try
            {
                if (!_ticksEmulators.ContainsKey(md.SecurityId))
                {
                    return;
                }
                Dictionary<string, List<Trade>> ticks = new();
                Random rnd = new Random();
                int _ticksPause = rnd.Next(MaxTicksRandomPause);
                if (lastTickCollection.AddSeconds(_ticksPause) > DateTime.Now)
                {
                    return;
                }
                var ask = md.Asks.Last();
                var bid = md.Bids.First();
                List<Trade> trades = new List<Trade>();

                int _maxTickCountInPack = rnd.Next(MaxTicksRandomCountInPack / 2) + 1;

                for (int i = 0; i < _maxTickCountInPack; i++)
                {
                    trades.Add(new Trade()
                    {
                        Price = ask.Price,
                        SecurityId = md.SecurityId,
                        Side = Side.Buy,
                        TransactionID = DateTime.Now.Ticks.ToString(),
                        Volume = (decimal)Math.Round(rnd.NextDouble() * (double)ask.Volume),
                        Time = md.Time.AddMilliseconds(10 * i),
                        IsOnline = true,
                        SecurityName = _ticksEmulators[md.SecurityId].Name
                    });

                    trades.Add(new Trade()
                    {
                        Price = bid.Price,
                        SecurityId = md.SecurityId,
                        Side = Side.Sell,
                        TransactionID = DateTime.Now.Ticks.ToString(),
                        Volume = (decimal)Math.Round(rnd.NextDouble() * (double)bid.Volume),
                        Time = md.Time.AddMilliseconds(30 * i),
                        IsOnline = true,
                        SecurityName = _ticksEmulators[md.SecurityId].Name
                    });
                }
                ticks.Add(md.SecurityId, trades);
                NewTickCollectionEvent?.Invoke(ticks);
                lastTickCollection = DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            
        }

        

        internal void Subscribe(Security security)
        {
            _depthEmulators.TryAdd(security.Id, security);
            if (!threadEmulating.IsAlive)
            {
                threadEmulating.Start();
            }
        }

        public void UnSubscribe(Security security)
        {
            _depthEmulators.Remove(security.Id, out var _);
        }

        public void SubscribeTick(Security security)
        {
            _ticksEmulators.TryAdd(security.Id, security);

            if (!threadEmulating.IsAlive)
            {
                threadEmulating.Start();
            }
        }

        public void UnSubscribeTick(Security security)
        {
            _ticksEmulators.Remove(security.Id, out var _);
        }

    }
}
