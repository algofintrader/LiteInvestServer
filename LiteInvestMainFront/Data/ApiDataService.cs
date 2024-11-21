﻿using RestSharp;
using LiteInvestServer.Json;
using System.Text.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using static System.Net.WebRequestMethods;
using Websocket.Client;

//TODO: провести рефакторинг (пока запихнул по быстрому)
//А так надо все объекты вывести нормально

using PlazaEngine.Entity;
using System.Web;

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

		private ConcurrentDictionary<string, SecurityApi> Securities { get; set; }
		ConcurrentDictionary<string, OrderBookService> OrderBookProcessors = new();

		string token = "";

		public Action<MarketDepthLevel, string, IEnumerable<MarketDepthLevel>> NewMarketDepth;
		public Action<string, List<TradeApi>> NewTicks;

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

			websocketClient.Start();
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

			websocketClient.MessageReceived.Subscribe(msg =>
			{
				//почему то не хочет десериализовывать стакан нормальнь
				var md = JsonConvert.DeserializeObject<MarketDepth>(msg.Text, new JsonSerializerSettings() { CheckAdditionalContent = true, });
				ProcessOrderBook(md);
			});

			websocketClient.Start();
			return websocketClient;
		}


		/// <summary>
		/// free lock структура для обработки стаканов. 
		/// </summary>
		/// <param name="md"></param>
		void ProcessOrderBook(MarketDepth md)
		{
			var secid = md.SecurityId;

			if (!OrderBookProcessors.ContainsKey(secid))
				OrderBookProcessors[secid] = new OrderBookService(Securities[secid], NewMarketDepth);

			OrderBookProcessors[secid].Process(md);

		}

		public int GetData()
		{
			return 1;
		}



	}
}