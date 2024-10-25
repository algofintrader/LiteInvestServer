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
        private Dictionary<string, MarketDepth> _marketDepths;
        private Thread threadEmulating;
        public DepthEmulator()
        {
            _depthEmulators = new ConcurrentDictionary<string, Security>();
            _marketDepths = new Dictionary<string, MarketDepth>();
            

            threadEmulating = new Thread(ThreadEmulating);
            threadEmulating.Name = "ThreadEmulating";
            threadEmulating.IsBackground = true;
            //threadEmulating.Start();
        }

        public event Action<MarketDepth>? MarketDepthChanged;

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

        internal void Subscribe(Security security)
        {
            _depthEmulators.TryAdd(security.Id, security);
            if (!threadEmulating.IsAlive)
            {
                threadEmulating.Start();
            }
        }

        internal void UnSubscribe(Security security)
        {
            _depthEmulators.Remove(security.Id, out var _);
        }


    }
}
