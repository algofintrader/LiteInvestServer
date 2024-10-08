
namespace PlazaEngine.Entity
{
    public interface IOrder
    {
        /// <summary>
        /// Комментарий к заявке, содержащий код клиента. 20 символов
        /// </summary>
        string Comment { get; }

        
        /// <summary>
        /// Биржевой номер Ордера, присваивается биржей
        /// </summary>
        string ExchangeOrderId { get; }

        /// <summary>
        /// Внутренний номер ордера, присваивается автоматически коннектором
        /// </summary>
        int NumberUserOrderId { get; }

        /// <summary>
        /// Код лицевого счета клиента зарегистрированного на бирже. 
        /// </summary>
        string PortfolioNumber { get; }

        /// <summary>
        /// Цена оредра
        /// </summary>
        decimal PriceOrder { get; }
        
        /// <summary>
        /// Реальная цена исполнения ордера
        /// </summary>
        decimal PriceReal { get; }

        /// <summary>
        /// Id инструмента
        /// </summary>
        string SecurityId { get; }

        /// <summary>
        /// Направление Ордера купля/продажа
        /// </summary>
        Side Side { get; }

        /// <summary>
        /// Состояние Ордера
        /// </summary>
        Order.OrderStateType State { get; }

        /// <summary>
        /// Время регистрации ордера, по серверу биржи
        /// </summary>
        DateTime TimeCallBack { get; }

        /// <summary>
        /// Время отмены ордера
        /// </summary>
        DateTime TimeCancel { get; }
        
        /// <summary>
        /// Время создания ордера присваивается в конструкторе, локальное
        /// </summary>
        DateTime TimeCreate { get; }

        /// <summary>
        /// Время исполнения ордера
        /// </summary>
        DateTime TimeDone { get; }

        /// <summary>
        /// Тип ордера
        /// </summary>
        Order.OrderType TypeOrder { get; }

        /// <summary>
        /// Объем выставленный по ордеру
        /// </summary>
        decimal Volume { get; }

    }
}