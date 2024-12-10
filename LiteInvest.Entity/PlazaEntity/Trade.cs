using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteInvest.Entity.PlazaEntity;

/// <summary>
/// Обезличенная сделка
/// </summary>
public class Trade
{

    public Trade() { }

    /// <summary>
    /// Собрать обезличенную сделку из сообщения биржи .
    /// </summary>
    /// <param name="replmsg">Сообщение от Биржи</param>
    /// <param name="isDealsOnline">В онлайне</param>
    /// <param name="securities">Словарь инструментов Id->Name </param>
    public Trade( ConcurrentDictionary<string, Security> securities)
    {
        Trade.securities = securities;
    }

    private static ConcurrentDictionary<string, Security>? securities;

    [JsonPropertyName("sn")]
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
        set
        {
            securityName = value;
        }
    }
    private string? securityName;

    [JsonPropertyName("tid")]
    /// Номер сделки
    /// </summary>
    public string TransactionID { get; set; }

    [JsonPropertyName("s")]
    /// <summary>
    /// Биржевой цифровой код инструмента
    /// </summary>
    public string SecurityId { get; set; }

    [JsonPropertyName("t")]
    /// <summary>
    /// Время сделки в часовом поясе MSK (utc+2)
    /// </summary>
    public DateTime Time {get => time; set { time = value; } }
    DateTime time;

    /// <summary>
    /// Сделка получена в онлайне
    /// </summary>
    public bool IsOnline { get => isOnline; set { isOnline = value; } }
    bool isOnline;

    [JsonPropertyName("d")]
    /// <summary>
    /// Направление сделки
    /// </summary>
    public Side Side { get => side; set { side = value; } }
    Side side;

    [JsonPropertyName("v")]
    /// <summary>
    /// Объем по сделке
    /// </summary>
    public decimal Volume { get => volume; set { volume = value; } }
    decimal volume;

    [JsonPropertyName("p")]
    /// <summary>
    /// Цена сделки
    /// </summary>
    public decimal Price { get => price; set { price = value; } }
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

