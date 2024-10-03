using Swashbuckle.AspNetCore.Annotations;

namespace LiteInvestServer.Records
{
    record UserCredentials
    {
        public required string Login { get; init; }
        public string Password { get; init; }

        /// <summary>
        /// Свободное имя, может быть ФИО
        /// </summary>
        public string FreeName { get; init; }
    };
}
