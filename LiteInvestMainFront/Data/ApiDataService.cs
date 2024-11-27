using RestSharp;
using LiteInvestServer.Json;
using System.Text.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.JavaScript;
using Newtonsoft.Json;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using static System.Net.WebRequestMethods;
using Websocket.Client;

//TODO: провести рефакторинг (пока запихнул по быстрому)
//А так надо все объекты вывести нормально

using PlazaEngine.Entity;
using System.Web;
using LiteInvestServer.Records;
using LiteInvestServer.Entity;

namespace LiteInvestMainFront.Data
{

	// сделал промежуточный класс
	public partial class TradeApi
	{
		[JsonProperty("sn")]
		public string SecurityName { get; set; }

		[JsonProperty("tid")]
		public string TransactionID { get; set; }

		[JsonProperty("s")]
		public string SecurityId { get; set; }

		[JsonProperty("t")]
		public DateTime Time { get; set; }

		[JsonProperty("d")]
		public Side Side { get; set; }

		[JsonProperty("v")]
		public decimal Volume { get; set; }

		[JsonProperty("p")]
		public decimal Price { get; set; }
	}

	public static class UriExtensions
	{
		/// <summary>
		/// Adds the specified parameter to the Query String.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="paramName">Name of the parameter to add.</param>
		/// <param name="paramValue">Value for the parameter to add.</param>
		/// <returns>Url with added parameter.</returns>
		public static Uri AddParameter(this Uri url, string paramName, string paramValue)
		{
			var uriBuilder = new UriBuilder(url);
			var query = HttpUtility.ParseQueryString(uriBuilder.Query);
			query[paramName] = paramValue;
			uriBuilder.Query = query.ToString();

			return uriBuilder.Uri;
		}
	}

	public class LoginInfo
	{
		public string token { get; set; }
		public DateTime expirationTime { get; set; }
	}

	public class ApiDataService
	{
		string mainadress = "http://188.72.77.60:3000/";
		Uri websocketurl = new Uri("ws://188.72.77.60:5000/");
		WebsocketClient websocketClient;

		RestClient client;

		//TODO: Можно тоже соединить напрямую с проектом сервера
		static string loginrequest = "Common/Login";
		static string getinstruments = "Trading/GetAllSecurities";
		static string openInstrument = "Trading/OpenInstrument";
		static string closeInstrument = "Trading/CloseInstrument";
		static string userInstuments = "Trading/GetUserInstruments";

		static string sendOrder = "Trading/SendOrder";
		static string cancelOrder = "Trading/CancelOrder";

		static string getMyOrders = "Trading/GetOrders";
		static string getOpenPositions = "Trading/GetOpenPositions";

		//GetOpenPositions
		//GetUserInstruments

		private ConcurrentDictionary<string, SecurityApi> Securities { get; set; }
		ConcurrentDictionary<string, OrderBookService> OrderBookProcessors = new();

		string token = "";

		public Action<MarketDepthLevel, MarketDepthLevel, string, Dictionary<decimal, MarketDepthLevel>> NewMarketDepth;
		public Action<string, List<TradeApi>> NewTicks;
		/// <summary>
		/// Мой новый ордер
		/// </summary>
		public Action<Order> NewMyOrder { get; set; }

		public List<Order> AllActiveOrders { get; set; }

		public ApiDataService()
		{
			client = new RestClient(mainadress);
			Securities = new ConcurrentDictionary<string, SecurityApi>();
			// websocketClient = new WebsocketClient(websocketurl);
		}

		//Хранилище Ордеров по инструментам? 
		//ConcurrentDictionary<string,>>

		public async Task<LoginInfo> LogIn(string login, string pass)
		{
			var request = new RestRequest(loginrequest)
				.AddQueryParameter("login", login, false)
				.AddQueryParameter("pass", pass, false);

			try
			{
				var response = await client.PostAsync<LoginInfo>(request);
				Console.WriteLine($"Token received {response.token}");
				token = response.token;
				return response;
			}
			catch (Exception ex)
			{
				return null;
			}
		}

		//TODO: Бардак с КЛИЕНТ ОРДЕРОМ и обычным ОРДЕРОМ

		public async Task<Order?> SendOrder(ClientOrder order)
		{

			var request = new RestRequest(sendOrder);

			request.AddHeader("liteinvest", token);
			request.AddBody(order);

			var response = await client.PostAsync(request);

			if (!response.IsSuccessful)
			{
				Console.WriteLine(response.ErrorMessage);
			}

			var r = JsonConvert.DeserializeObject<Order>(response.Content);

			return r;

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}

		public async Task<ClientOrder?> CancelOrder(Order order)
		{

			var request = new RestRequest(cancelOrder);

			request.AddHeader("liteinvest", token);
			request.AddBody(order);

			var response = await client.PostAsync(request);

			if (!response.IsSuccessful)
			{
				Console.WriteLine(response.ErrorMessage);
			}

			var r = JsonConvert.DeserializeObject<ClientOrder>(response.Content);

			return r;

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}
		public async Task<List<Order>> GetOrders()
		{

			try
			{
				var request = new RestRequest(getMyOrders);

				request.AddHeader("liteinvest", token);

				var response = await client.GetAsync(request);

				if (!response.IsSuccessful)
				{
					Console.WriteLine(response.ErrorMessage);
				}

				var r = JsonConvert.DeserializeObject<List<Order>>(response.Content);

				return r;
			}
			catch (Exception ex)
			{
				return null;
			}

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}

		public async Task<List<Pos>> GetPositions()
		{

			try
			{
				var request = new RestRequest(getOpenPositions);

				request.AddHeader("liteinvest", token);

				var response = await client.GetAsync(request);

				if (!response.IsSuccessful)
				{
					Console.WriteLine(response.ErrorMessage);
				}

				var r = JsonConvert.DeserializeObject<List<Pos>>(response.Content);

				return r;
			}
			catch (Exception ex)
			{
				return null;
			}

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}



		public async void OpenInstrument(SecurityApi sec)
		{

			var request = new RestRequest(openInstrument);

			//request.AddCookie("liteinvest", token,"/", "188.72.77.60:3000");


			request.AddHeader("liteinvest", token);
			request.AddBody(sec);

			var response = await client.PostAsync(request);

			if (!response.IsSuccessful)
			{
				Console.WriteLine(response.ErrorMessage);
			}

			// var r = await JsonSerializer.DeserializeAsync<SecurityApi>(response.st);

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}

		public async Task CloseInstrument(SecurityApi sec)
		{

			var request = new RestRequest(closeInstrument);

			//request.AddCookie("liteinvest", token,"/", "188.72.77.60:3000");
			request.AddHeader("liteinvest", token);
			request.AddBody(sec);

			var response = await client.PostAsync(request);

			if (!response.IsSuccessful)
			{
				Console.WriteLine(response.ErrorMessage);
			}

			// var r = await JsonSerializer.DeserializeAsync<SecurityApi>(response.st);
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}

		public async Task<IEnumerable<SecurityApi>> GetUserInstruments()
		{

			var request = new RestRequest(userInstuments);

			//request.AddCookie("liteinvest", token,"/", "188.72.77.60:3000");
			request.AddHeader("liteinvest", token);

			var response = await client.GetAsync(request);

			if (!response.IsSuccessful)
			{
				Console.WriteLine(response.ErrorMessage);
				return null;
			}

			// var r = await JsonSerializer.DeserializeAsync<SecurityApi>(response.st);

			var instruments = JsonConvert.DeserializeObject<IEnumerable<SecurityApi>>(response.Content);

			return instruments;

		}

		public async Task<IEnumerable<SecurityApi>> GetInstruments()
		{

			var request = new RestRequest(getinstruments);

			//request.AddCookie("liteinvest", token,"/", "188.72.77.60:3000");
			request.AddHeader("liteinvest", token);

			var response = await client.GetAsync(request);

			if (!response.IsSuccessful)
			{
				Console.WriteLine(response.ErrorMessage);
				return null;
			}

			// var r = await JsonSerializer.DeserializeAsync<SecurityApi>(response.st);

			var instruments = JsonConvert.DeserializeObject<IEnumerable<SecurityApi>>(response.Content);

			foreach (var instr in instruments)
				Securities[instr.id] = instr;

			return instruments;

		}

		//TODO: отписки нет толком

		public async Task<WebsocketClient> SubcribeTick(string secid)
		{

			var webscoketrequest =
				websocketurl
					.AddParameter("stream", "public_trades")
					.AddParameter("sec_id", secid)
					.AddParameter("liteinvest", token);


			websocketClient = new WebsocketClient(webscoketrequest);

			websocketClient.MessageReceived.Subscribe(msg =>
			{
				try
				{
					// var ticks = JsonConvert.DeserializeObject<List<Trade>>(msg.Text);
					var ticks = JsonConvert.DeserializeObject<List<TradeApi>>(msg.Text);
					NewTicks?.Invoke(ticks[0].SecurityId, ticks);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.ToString());
				}

			});

			Console.WriteLine(webscoketrequest);

			await websocketClient.Start();
			return websocketClient;

		}



		/// <summary>
		/// Пока что сделал отписку по самому простому принципу. 
		/// </summary>
		/// <param name="secid"></param>
		/// <returns></returns>
		public async Task<WebsocketClient> SubscribeOrderBook(string secid)
		{
			var webscoketrequest =
				websocketurl
					.AddParameter("stream", "orderbook")
					.AddParameter("sec_id", secid)
					.AddParameter("liteinvest", token);

			websocketClient = new WebsocketClient(webscoketrequest);

			websocketClient.MessageReceived.Subscribe(async msg =>
			{
				try
				{
					//почему то не хочет десериализовывать стакан нормальнь
					var md = JsonConvert.DeserializeObject<MarketDepth>(msg.Text,
						new JsonSerializerSettings() { CheckAdditionalContent = true, });

					ProcessOrderBook(md);
				}
				catch (Exception ex)
				{

				}
			});


			Console.WriteLine(webscoketrequest);

			await websocketClient.Start();
			return websocketClient;
		}

		bool ordersubscribed = false;

		public async Task<WebsocketClient> SubscribePrivateOrders()
		{

			if (ordersubscribed)
				return null;


			ordersubscribed = true;

			var webscoketrequest =
				websocketurl
					.AddParameter("stream", "my_orders")
					.AddParameter("liteinvest", token);

			websocketClient = new WebsocketClient(webscoketrequest);

			Console.WriteLine("Order socket "+ webscoketrequest);

			websocketClient.MessageReceived.Subscribe(async msg =>
			{
				try
				{
					//почему то не хочет десериализовывать стакан нормальнь
					var order = JsonConvert.DeserializeObject<Order>(msg.Text, new JsonSerializerSettings() { CheckAdditionalContent = true, });
					NewMyOrder?.Invoke(order);

				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			});


			Console.WriteLine(webscoketrequest);

			await websocketClient.Start();
			return websocketClient;
		}

		public void StopOrderBookProcesor(string secId)
		{
			if(OrderBookProcessors.TryRemove(secId, out var _))
				Console.WriteLine("REMOVED "+secId);
		}

		/// <summary>
		/// free lock структура для обработки стаканов. 
		/// </summary>
		/// <param name="md"></param>
		void ProcessOrderBook(MarketDepth md)
		{
			var secid = md.SecurityId;

			// обычный вариант
			var asklevels = md.Asks;
			var bidlevels = md.Bids;

			var bestask = asklevels[asklevels.Count - 1];
			var bestbid = bidlevels[0];

			//проще кинуть скорее всего просто Dictionary c типом

			var count = asklevels.Count; // - это середина получается

			decimal maxlevel = asklevels[0].Price + 20 * Securities[secid].PriceStep;
			decimal minlevel = bidlevels[bidlevels.Count-1].Price - 20 * Securities[secid].PriceStep;


			//Console.WriteLine($"Макс {maxlevel} BestAsk= {asklevels[count - 1].Price} BestBID = {bidlevels[0].Price} Мин {minlevel}");
			ConcurrentDictionary<decimal, MarketDepthLevel> AllLevels = new();

			for (decimal i = maxlevel; i > minlevel; i -=  Securities[secid].PriceStep)
			{
				bool red = i > bestbid.Price;

				// Console.WriteLine("Уровень =" + i);
				AllLevels[i] = new MarketDepthLevel()
				{
					Price = i,
					Type = red? Side.Sell: Side.Buy,
				};
			}

			List<MarketDepthLevel> sorted2 = new List<MarketDepthLevel>();

			foreach (var asklevel in asklevels)
			{
				sorted2.Add(asklevel);

				//if (asklevel.Price < maxlevel)
					AllLevels[asklevel.Price] = asklevel;
			}


			foreach (var bidlevel in bidlevels)
			{
				sorted2.Add(bidlevel);

				//if (bidlevel.Price > minlevel)
					AllLevels[bidlevel.Price] = bidlevel;
			}



			//TODO: возможно стоит взять другой вариант, поработать со стринг, тогда может он не перемешает это все в кашу. 
			var sorted =
				AllLevels.OrderByDescending(s => s.Value.Price)
				.ToDictionary(x => x.Key, x => x.Value);
			NewMarketDepth?.Invoke(bestbid,bestask, secid, sorted);

		}

		public int GetData()
		{
			return 1;
		}



	}
}
