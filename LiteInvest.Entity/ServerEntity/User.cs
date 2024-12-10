using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LiteInvest.Entity.ServerEntity
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
        public decimal AllBalance { get; set; }

        // до конца не понятно как это реализовать 
        //public decimal FreeBalance { get; set; }

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
        ///  Сколько денег выделено на торвголю
        /// </summary>
        public decimal Limit { get; set; }

        [DataMember] public List<SecurityApi> OpenedInstruments { get; set; } = new ();

        /// <summary>
        /// Плечо на фондовый
        /// </summary>
        //public int LeverageFond { get; set; }

        /// <summary>
        /// Плечо на Фючерсы
        /// </summary>
        public int LeverageFutures { get; set; }

        /// <summary>
        /// Плечо на Валюту
        /// </summary>
        //public int LeverageCurrency { get; set; }






    }
}
