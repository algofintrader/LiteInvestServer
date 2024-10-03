namespace LiteInvestServer.Json
{
    public class SecurityApi
    {
        public string id { get; set; }
        public string ShortName { get; set; }

        public string ClassCode { get; set; }
        public string FullName { get; set; }
        public string Type { get; set; }

        public decimal Lot { get; set; }
        public decimal PriceStep { get; set; }

        public decimal Decimals { get; set; }

        public decimal PriceLimitLow { get; set; }
        public decimal PriceLimitHigh { get; set; }
    }
}
