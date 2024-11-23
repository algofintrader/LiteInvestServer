using LiteInvestServer.Json;
using PlazaEngine.Entity;
using System.Net.WebSockets;
using Websocket.Client;


namespace LiteInvestMainFront.Data
{

	/// <summary>
	/// По идее вообще этот сервис хранит всю инорфмацию обо всем. 
	/// </summary>
	public class WorkWindowService:IDisposable
	{
		/// <summary>
		/// максимальный размер из тех, что щас есть в тиках. 
		/// </summary>
		public decimal currentMaxVolume { get; set; }
		public SecurityApi securityApi { get; set; }

		public decimal minchart { get; set; }

		public decimal maxchart { get; set; }

		public List<TradeApi> Ticks { get; set; } = new List<TradeApi>();

		public IEnumerable<MarketDepthLevel> Quotes { get; set; } = new List<MarketDepthLevel>();

		public List<WebsocketClient> WebSockets { get; set; } = new List<WebsocketClient>();


		public int levelsFromBest = 20;

		
		public int start = 0;

		public DateTime dt { get; set; }

		public ApiDataService ApiDataService { get; set; }

		public Action? QuotesRefresh;
		public Action? TicksRefresh;

		public async void Start()
		{
			dt = DateTime.Now;

			ApiDataService.NewMarketDepth += OnNewMarketDepth;
			ApiDataService.NewTicks += OnNewTicks;

			WebSockets.Add(await ApiDataService.SubscribeOrderBook(securityApi.id));
			WebSockets.Add(await ApiDataService.SubcribeTick(securityApi.id));

			Console.WriteLine($"Started WebSocketService {securityApi.id}");
		}

		public async void Stop()
		{
			
			ApiDataService.NewMarketDepth -= OnNewMarketDepth;
			ApiDataService.NewTicks -= OnNewTicks;

			ApiDataService.StopOrderBookProcesor(securityApi.id);

			foreach (var webSocket in WebSockets.ToList())
			{
				try
				{
					var r = await webSocket.StopOrFail(WebSocketCloseStatus.NormalClosure, "Закрыта вкладка");

					if (!r)
					{
						Console.WriteLine($"Error stopping socket {securityApi.id}");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}

			Console.WriteLine($"Stopped! WebSocketService {securityApi.id}");
		}

		private void OnNewTicks(string secId, List<TradeApi> ticks)
		{
			if (securityApi.id != secId) return;

			if (ticks == null)
				return;

			//Console.WriteLine(ticks[0].SecurityId + " new tick !" + ticks[0].Price);
			ProcessTicks(ticks);
		}

		private void OnNewMarketDepth(MarketDepthLevel bestbid, string secinstrument, IEnumerable<MarketDepthLevel> md)
		{
			try
			{

				if (securityApi.id != secinstrument) return;

				if (md == null)
					return;

				Quotes = md;

				//TODO - при масштабированини этот уровень лучше менять. 
				maxchart = bestbid.Price + levelsFromBest * securityApi.PriceStep;
				minchart = bestbid.Price - levelsFromBest * securityApi.PriceStep;

				//Console.WriteLine($"MD recevied {secutityMain.id} NAME={secutityMain.Isin}");

				var dictionary = Quotes.Select(t => new { t.Price, t })
					.ToDictionary(t => t.Price, t => t);


				//int visibleitem = Grid.GetStartRowVisibleIndex();
				//Console.WriteLine("видимая строка " + Quotes.ToArray()[visibleitem].Price + $@"к1={Grid.GetVisibleRowCount()} к2={Quotes.Count()}");

				QuotesRefresh?.Invoke();
			}
			catch (Exception ex)
			{

			}
		}

		public void ProcessTicks(List<TradeApi> ticks)
		{
			foreach (var tick in ticks)
			{
				start++;
				tick.Time = dt + TimeSpan.FromSeconds(start);
				Ticks.Add(tick);

				if (Ticks.Count > 15) Ticks.RemoveAt(0);
			}

			currentMaxVolume = Ticks.Max(s => s.Volume);

			TicksRefresh?.Invoke();
		}

		public void Dispose()
		{
			QuotesRefresh = null;
			TicksRefresh = null;
			// TODO release managed resources here
		}
	}
}
