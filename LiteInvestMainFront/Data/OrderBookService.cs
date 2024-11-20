using LiteInvestServer.Json;
using PlazaEngine.Entity;
using System.Collections.Concurrent;

namespace LiteInvestMainFront.Data
{
    public class OrderBookService
    {
        public SecurityApi Sec { get; private set; }
        public Action<MarketDepthLevel, string, IEnumerable<MarketDepthLevel>> Mdupdated { get; }

        private int levelsCount = 300;

        public OrderBookService(SecurityApi sec, Action<MarketDepthLevel, string, IEnumerable<MarketDepthLevel>> mdupdated)
        {
            Sec = sec;
            Mdupdated = mdupdated;
        }
        //сначала проставим просто пустые зоны


        //допустил ошибки в симуляции
        private bool simulation = true;

        public void Process(MarketDepth md)
        {
            //биды идут инверсивно с большего к малому


            // обычный вариант
           // var asklevels = md.Asks;
            //var bidlevels = md.Bids;

            
			 md.Asks.ForEach(b =>
			 {
				 b.Bid = b.Ask;
				 b.Ask = 0;
			 });

			 md.Bids.ForEach(b =>
			 {
				 b.Ask=b.Bid;
				 b.Bid = 0;
			 });

			 var asklevels = md.Bids;
			 var bidlevels = md.Asks;

            var bestbid = bidlevels[0];

            //проще кинуть скорее всего просто Dictionary c типом

            var count = asklevels.Count; // - это середина получается

            decimal maxlevel = asklevels[count - 1].Price + levelsCount * Sec.PriceStep;
            decimal minlevel = bidlevels[0].Price - levelsCount * Sec.PriceStep;


            Console.WriteLine($"Макс {maxlevel} BestAsk= {asklevels[count - 1].Price} BestBID = {bidlevels[0].Price} Мин {minlevel}");
            ConcurrentDictionary<decimal, MarketDepthLevel> AllLevels = new();

            for (decimal i = maxlevel; i > minlevel; i -= Sec.PriceStep)
            {

                // Console.WriteLine("Уровень =" + i);
                AllLevels[i] = new MarketDepthLevel()
                {
                    Price = i
                };
            }

            List<MarketDepthLevel> sorted2 = new List<MarketDepthLevel>();

            foreach (var asklevel in asklevels)
            {
                sorted2.Add(asklevel);

                if (asklevel.Price < maxlevel)
                    AllLevels[asklevel.Price] = asklevel;
            }


            foreach (var bidlevel in bidlevels)
            {
                sorted2.Add(bidlevel);

                if (bidlevel.Price > minlevel)
                    AllLevels[bidlevel.Price] = bidlevel;
            }



            //TODO: возможно стоит взять другой вариант, поработать со стринг, тогда может он не перемешает это все в кашу. 
            var sorted = AllLevels.Values.OrderByDescending(s => s.Price);
            

			Mdupdated?.Invoke(bestbid, Sec.id, sorted);
        }


    }
}
