using PlazaEngine.Engine;

using ru.micexrts.cgate.message;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace PlazaEngine.Entity;

/// <summary>
/// Обезличенная сделка
/// </summary>
public class Trade
{

    //public Trade() { }

    /// <summary>
    /// Собрать обезличенную сделку из сообщения биржи .
    /// </summary>
    /// <param name="replmsg">Сообщение от Биржи</param>
    /// <param name="isDealsOnline">В онлайне</param>
    /// <param name="securities">Словарь инструментов Id->Name </param>
    public Trade(StreamDataMessage replmsg, bool isDealsOnline, ConcurrentDictionary<string, Security> securities)
    {
        TransactionID = replmsg["id_deal"].asLong().ToString();
        SecurityId = replmsg["isin_id"].asInt().ToString();
        if (securities.TryGetValue(replmsg["isin_id"].asInt().ToString(), out Security? _secname))
            securityName = _secname.Name;
        time = replmsg["moment"].asDateTime();
        isOnline = isDealsOnline;
        side = replmsg["public_order_id_buy"].asLong() > replmsg["public_order_id_sell"].asLong() ? Side.Buy : Side.Sell;
        volume = replmsg["xamount"].asInt();
        price = Convert.ToDecimal(replmsg["price"].asDecimal());
        
        Trade.securities = securities;
    }

    private static ConcurrentDictionary<string, Security>? securities;


    /// <summary>
    /// Имя инструмента
    /// </summary>
    public string SecurityName 
    { 
        get 
        {
            if (!(securityName is null) && securityName.Length > 0)
            {
                return securityName;
            }
            else 
            {
                if ((securities?.Count ?? 0) > 0 && (securities?.TryGetValue(SecurityId, out Security? _secname)??false))
                {
                    securityName = _secname.Name;
                }
                return securityName??default;
            }
        } 
    
    }
    private string? securityName;

    /// <summary>
    /// Номер сделки
    /// </summary>
    public string TransactionID { get; set; }

    /// <summary>
    /// Биржевой цифровой код инструмента
    /// </summary>
    public string SecurityId { get; set; }

    /// <summary>
    /// Время сделки в часовом поясе MSK (utc+2)
    /// </summary>
    public DateTime Time {get => time;}
    DateTime time;

    /// <summary>
    /// Сделка получена в онлайне
    /// </summary>
    internal bool IsOnline { get => isOnline; }
    bool isOnline;

    /// <summary>
    /// Направление сделки
    /// </summary>
    public Side Side { get => side; }
    Side side;

    /// <summary>
    /// Объем по сделке
    /// </summary>
    public decimal Volume { get => volume; }
    decimal volume;

    /// <summary>
    /// Цена сделки
    /// </summary>
    public decimal Price { get => price; }
    decimal price;


    /// <summary>
    /// to take a line to save
    /// взять строку для сохранения
    /// </summary>
    /// <returns>line with the state of the object/строка с состоянием объекта</returns>
    public override string ToString()
    {
       return new StringBuilder()
            .Append(SecurityName!=null ? SecurityName : "").Append("; ")
            .Append(SecurityId).Append("; ")
            .Append(Time.ToString("yyyy.MM.dd; HH:mm:ss.fff")).Append("; ")
            .Append(Price.ToString()).Append("; ")
            .Append(Volume.ToString()).Append("; ")
            .Append(Side == Side.Buy ? "Buy" : "Sell").Append("; ")
            .Append(TransactionID).Append("; ")
            .Append(isOnline.ToString()).Append("; ")
            .ToString();
    }
}

