using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Runtime.Serialization;

namespace LiteInvestServer.Entity
{

    public enum Roles
    {
        Usual,
        Admin
    }


    [DataContract]
    public class User
    {
        public User(string _userName, string _pass)
        {

            userName = _userName;
            Password = _pass;
        }

        [DataMember]
        public string userName { get; private set; }

        [DataMember]
        private string Password { get; set; }
        [DataMember]
        public decimal Balance { get; set; }


        [DataMember]
        /// <summary>
        /// Может ли данный юзер торговать
        /// Будет отключено в админке.
        /// </summary>
        public bool CanTrade { get; set; }

        [DataMember]
        /// <summary>
        ///  Не совсем до конца понятно как это считать
        /// </summary>
        public decimal LockedBalance { get; set; }

        [DataMember]
        /// <summary>
        ///  Лимит на торговлю
        /// </summary>
        public decimal Limit { get; set; }

        public ConcurrentDictionary <string, Order> Orders { get; set; }

        public ConcurrentDictionary<string, Position> Users { get;}


    }
}
