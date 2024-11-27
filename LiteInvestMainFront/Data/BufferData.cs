using System.Collections.Concurrent;
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

		public int AgregationStep { get; set; } = 0;

		/// <summary>
		/// Хранилище ордеров по инструментам получается
		/// </summary>
		public ConcurrentDictionary<decimal, Order> Orders { get; set; } = new();
		public List<TradeApi> Ticks { get; set; } = new List<TradeApi>();



	}
}
