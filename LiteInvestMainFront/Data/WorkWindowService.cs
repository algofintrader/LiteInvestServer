using LiteInvestServer.Json;
using PlazaEngine.Entity;
using Websocket.Client;

namespace LiteInvestMainFront.Data
{

	/// <summary>
	/// По идее вообще этот сервис хранит всю инорфмацию обо всем. 
	/// </summary>
	public class WorkWindowService
	{
		public SecurityApi securityApi { get; set; }

		public decimal minchart { get; set; }

		public decimal maxchart { get; set; }

		public List<TradeApi> Ticks { get; set; } = new List<TradeApi>();

		public IEnumerable<MarketDepthLevel> Quotes { get; set; } = new List<MarketDepthLevel>();

		public List<WebsocketClient> WebSockets { get; set; } = new List<WebsocketClient>();


		public int levelsFromBest = 20;

		int i = 1;
		int orderbookcount = 0;
		public int start = 0;

		public DateTime dt { get; set; }

	}
}
