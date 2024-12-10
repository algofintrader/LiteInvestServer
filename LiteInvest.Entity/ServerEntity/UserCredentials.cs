namespace LiteInvest.Entity.ServerEntity
{
	public record UserCredentials
    {
        public required string LoginEmail { get; init; }
        public required string Password { get; init; }

        /// <summary>
        /// Свободное имя, может быть ФИО
        /// </summary>
        public string FreeName { get; init; }
    };
}
