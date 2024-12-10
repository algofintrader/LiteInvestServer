using LiteInvest.Entity.PlazaEntity;
using Newtonsoft.Json;

namespace BlazorRenderAuto.Client.Entity
{
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
}
