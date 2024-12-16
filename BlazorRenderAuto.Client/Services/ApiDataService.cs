using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Web;
using LiteInvest.Entity.PlazaEntity;
using LiteInvest.Entity.ServerEntity;

using RestSharp;
using BlazorRenderAuto.Client.Entity;
using Websocket.Client;

namespace BlazorRenderAuto.Client.Services
{

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

	public class ApiDataService:IDisposable
	{
		//TODO: перенести все нормально в настройки


		string mainadress = "http://188.72.77.60:3000/";
		Uri websocketurl = new Uri("ws://188.72.77.60:5000/");
		//WebsocketClient websocketClient;

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

		string token = "";

		public Action<string, List<TradeApi>> NewTicks;

		/// <summary>
		/// Мой новый ордер
		/// </summary>
		public Action<Order> NewMyOrder { get; set; }

		public List<Order> AllActiveOrders { get; set; }
		public WebsocketClient orderwebcocketclient { get; private set; }

		private ConcurrentDictionary<int, WebsocketClient> AllWebSockets = new();

		public Action<List<MarketDepthLevel>, List<MarketDepthLevel>,string> NewQuotes { get; set; }

		public ApiDataService()
		{
			client = new RestClient(mainadress);
			Securities = new ConcurrentDictionary<string, SecurityApi>();
			// websocketClient = new WebsocketClient(websocketurl);
		}


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

		public async Task<Order?> SendOrder(ClientOrder order)
		{
			try
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
			}
			catch (Exception ex) { Console.WriteLine(ex.Message); return null; }

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}

		public async Task<ClientOrder?> CancelOrder(Order order)
		{
			try
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
			}
			catch (Exception ex) { Console.WriteLine(ex.Message); return null; }

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}

		public async Task<List<Order>> GetActiveOrders()
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
				Console.WriteLine($"{ex.Message}");
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
				Console.WriteLine(ex.Message);
				return null;
			}

			//TODO: Сделать десериализацию нормальную 
			//var answer = JsonConvert.DeserializeObject(response.Content);
		}

		public Action<SecurityApi> NewSecOpened { get; set; }


		
		public Task OpenInstrument(SecurityApi sec)
		{

			var clone = (SecurityApi)sec.Clone();

			clone.SpecialHash = DateTime.Now.GetHashCode().ToString();
			NewSecOpened?.Invoke(clone);

			return Task.CompletedTask;
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

		public async Task<int?> SubcribeTick(string secid)
		{
			try
			{
				var webscoketrequest =
					websocketurl
						.AddParameter("stream", "public_trades")
						.AddParameter("sec_id", secid)
						.AddParameter("liteinvest", token);


				var websocketClient = new WebsocketClient(webscoketrequest);

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

				var hash = websocketClient.GetHashCode();
				AllWebSockets.TryAdd(hash, websocketClient);
				await websocketClient.Start();
				return hash;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				return null;
			}

		}

		
		public async Task<int?> SubscribeOrderBook(string secid)
		{
			try
			{
				var webscoketrequest =
					websocketurl
						.AddParameter("stream", "orderbook")
						.AddParameter("sec_id", secid)
						.AddParameter("liteinvest", token);

				var websocketClient = new WebsocketClient(webscoketrequest);

				websocketClient.MessageReceived.Subscribe(async msg =>
				{
					try
					{
						//почему то не хочет десериализовывать стакан нормальнь
						var md = JsonConvert.DeserializeObject<MarketDepth>(msg.Text,
							new JsonSerializerSettings() { CheckAdditionalContent = true, });

						NewQuotes?.Invoke(md.Bids,md.Asks, md.SecurityId);
						
					}
					catch (Exception ex)
					{

					}
				});


				Console.WriteLine(webscoketrequest);

				var hash = websocketClient.GetHashCode();
				AllWebSockets.TryAdd(hash, websocketClient);

				await websocketClient.Start();
				return hash;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return null;
			}
		}

		public async void StopWebSocket(int? websocketId)
		{
			if (websocketId == null)
			{
				Console.WriteLine("NUll WEBSOCKET!");
				return;
			}

			var websocket = AllWebSockets.TryGetValue((int)websocketId, out var foundwebsocket);

			if (foundwebsocket == null)
			{
				Console.WriteLine("No WEBSOCKET FOUND With HASH!");
				return;
			}

			var res = await foundwebsocket.StopOrFail(status: WebSocketCloseStatus.NormalClosure, "");

			Console.WriteLine(!res
				? $"Problems with closing websocket ID {websocketId}"
				: $"Closed success {websocketId}");

			AllWebSockets.Remove((int)websocketId,out var _);
		}

		public void Dispose()
		{
			client = null;
			Securities = null;
			GC.SuppressFinalize(this);
		}
	}

	
}
