using System.Runtime.Serialization;

namespace LiteInvestServer.Json
{
    [DataContract]
    public class SecurityApi:ICloneable
    {
        [DataMember]
        public string id { get; set; }
        [DataMember]
        public string Isin { get; set; }

        [DataMember]
        public string ShortName { get; set; }
        [DataMember]
        public string ClassCode { get; set; }
        [DataMember]
        public string FullName { get; set; }
        [DataMember]
        public string Type { get; set; }
        [DataMember]
        public decimal Lot { get; set; }
        [DataMember]
        public decimal PriceStep { get; set; }
        [DataMember]
        public decimal Decimals { get; set; }
        [DataMember]
        public decimal PriceLimitLow { get; set; }
        [DataMember]
        public decimal PriceLimitHigh { get; set; }
		[DataMember]
		public string SpecialHash { get; set; }

		public object Clone()
		{
            return new SecurityApi()
            {
                id = id,
                Isin = Isin,
                ShortName = ShortName,
                ClassCode = ClassCode,
                FullName = FullName,
                Type = Type,
                Lot = Lot,
                PriceStep = PriceStep,
                Decimals = Decimals,
                PriceLimitLow = PriceLimitLow,
                PriceLimitHigh = PriceLimitHigh,
             
            };
		}
	}
}
