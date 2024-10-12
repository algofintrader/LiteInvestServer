using PlazaEngine.Entity;
using Swashbuckle.AspNetCore.Annotations;

namespace LiteInvestServer.Records
{
    public record ClientOrder
    {
        [SwaggerSchema("Уникальный идентификатор инструмента")]
        public required string SecID { get; init; }
        public required Side Side { get; init; }
        public required int Volume { get; init; }
        public decimal? Price { get; init; }

        [SwaggerSchema("Market = true. Limit = false")]
        public bool Market { get; set; } = false;

        public int? NumberOrderId { get; set; }

    };
}
