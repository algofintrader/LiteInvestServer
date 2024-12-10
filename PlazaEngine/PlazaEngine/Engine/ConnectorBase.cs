

using System.Collections.Concurrent;
using LiteInvest.Entity.PlazaEntity;

namespace PlazaEngine.Engine
{
    public class ConnectorBase
    {
        /// <summary>
        /// Key SecurityId
        /// </summary>
        public ConcurrentDictionary<string, Security> Securities = new ConcurrentDictionary<string, Security>();

        /// <summary>
        /// 
        /// </summary>
        internal ConcurrentDictionary<string, string> _securities = new();
    }
}