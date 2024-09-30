using PlazaEngine.Entity;

using System.Collections.Concurrent;

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