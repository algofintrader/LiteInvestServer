using LiteInvestServer.Json;
using System.Collections.Concurrent;

namespace BlazorApp3.Data
{
	public class BufferService
	{
		public IEnumerable<SecurityApi> Securities { get; set; }
		public SecurityApi securityApiSelected;
		public ConcurrentDictionary<string, SecurityApi> SecuritiesRegiestered = new();
		public bool UserLoggedIn = false;

		public Action OnComboBoxValuePicked;
		public Action OnLogin;

        public bool windowVisible = false;
    }
}
