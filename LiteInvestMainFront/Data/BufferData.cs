using DevExpress.Blazor;
using PlazaEngine.Entity;

namespace LiteInvestMainFront.Data
{
	public class BufferData
	{

		public decimal minchart { get; set; }
		public decimal maxchart { get; set; }

		public BufferData()
		{

		}

		public int levelsFromBest = 20;

		public DateTime dt { get; set; }

		public int orderbookcount = 0;
		public int start = 0;

		

		public List<TradeApi> Ticks { get; set; } = new List<TradeApi>();

		public void ProcessTick(List<TradeApi> ticks)
		{
			foreach (var tick in ticks)
			{
				start++;
				tick.Time = dt + TimeSpan.FromSeconds(start);
				Ticks.Add(tick);

				try
				{
					if (Ticks.Count > 15) Ticks.RemoveAt(0);
				}
				catch (Exception ex)
				{

				}
			}
		}


	}
}
