using Swashbuckle.AspNetCore.Annotations;

namespace LiteInvestServer.Records
{
    record UserCredentials
    {
        public required string LoginEmail { get; init; }
        public required string Password { get; init; }

        /// <summary>
        /// Свободное имя, может быть ФИО
        /// </summary>
        public string FreeName { get; init; }
    };
}
