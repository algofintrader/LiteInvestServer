using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

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
        public User(string _login, string _pass)
        {

            Login = _login;
            Password = _pass;
        }

        [DataMember]
        public string Login { get; private set; }

        //TODO: pass HASH
        [JsonIgnore]
        [DataMember]
        public string Password { get; set; }
        [DataMember]
        public decimal Balance { get; set; }

        //NOTE: PROFIT


        [DataMember]
        /// <summary>
        /// Может ли данный юзер торговать
        /// Будет отключено в админке.
        /// </summary>
        public bool CanTrade { get; set; }

        [JsonIgnore]
        [DataMember]
        public bool Admin { get; set; }

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

    }
}
