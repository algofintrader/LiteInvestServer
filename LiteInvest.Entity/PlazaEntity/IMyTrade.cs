


namespace LiteInvest.Entity.PlazaEntity
{
    /// <summary>
    /// customer transaction on the exchange
    /// Клиентская сделка, совершённая на бирже
    /// </summary>
    public interface IMyTrade
    {
        /// <summary>
        /// Код лицевого счета сделки
        /// </summary>
        public string Portfolio { get; }

        /// <summary>
        /// Комментарий к сделке, перешедший из ордера
        /// </summary>
        string Comment { get; }

        /// <summary>
        /// parent's warrant number
        /// Биржевой номер ордера родителя
        /// </summary>
        string NumberOrderMarket { get; }

        /// <summary>
        /// Внутренний номер ордера, по которому прошла сделка
        /// </summary>
        int NumberOrderUser { get; }

        /// <summary>
        ///  trade number
        /// номер сделки в торговой системе
        /// </summary>
        string NumberTrade { get; }

        /// <summary>
        /// price
        /// цена
        /// </summary>
        decimal Price { get; }

        /// <summary>
        /// instrument code
        /// Биржевой код инструмента по которому прошла сделка
        /// </summary>
        string SecurityId { get; }

        /// <summary>
        /// party to the transaction
        /// Сторона сделки
        /// </summary>
        Side Side { get; }

        /// <summary>
        /// time
        /// время сделки
        /// </summary>
        DateTime Time { get; }

        /// <summary>
        /// volume
        /// объём
        /// </summary>
        decimal Volume { get; }


        /// <summary>
        /// Строковое представление
        /// </summary>
        /// <returns></returns>
        string ToString();
    }
}