using System.ComponentModel;
using System.Globalization;
using System.Runtime.Serialization;

namespace PlazaEngine.Entity;

/// <summary>
/// security
/// инструмент
/// </summary>
/// 
public record Security(
    string ShortName,
    string FullName,
    SecurityType Type,
    string ClassCode,
    decimal Lot)
    : INotifyPropertyChanged
{
 

    /// <summary>
    /// Уникальный идентификатор/ключ. 
    /// Любое упоминание Security ссылается на это свойство.
    /// Запросить Security можно использовав это свойство в качестве ключа.
    /// </summary>
    public required string Id { get; init; }
 
    public required string Name { get; init; }

    private decimal _bestask;
    
    
    public decimal BestAsk 
    {
        get => _bestask;
        set 
        { _bestask = value; PropertyEvent("BestAsk"); }
    }

    private decimal _bestbid;

    
    public decimal BestBid
    {
        get => _bestbid;
        set { _bestbid = value; PropertyEvent("BestBid"); }
    }
    

    
    public List<MarketDepthLevel> Bids { get; set; }
    public List<MarketDepthLevel> Asks { get; set; }

    

    public DateTime QuotesUpdateTime { get; set; }

    

    public void UpdateBidsAsks (MarketDepth depth)
    {
        if (depth.Bids.Count > 0)
            BestBid = depth.Bids.First().Price;
        
        if (depth.Asks.Count > 0)
            BestAsk = depth.Asks.Last().Price;

        Bids = depth.Bids;
        Asks = depth.Asks;

        QuotesUpdateTime = DateTime.Now;
       
       // DepthUpdated?.Invoke(depth);
    }
    
    /// <summary>
    /// Событие обновление всех котировок
    /// Написал специально для Plaza2
    /// В один момент обновляются BestAsk, BestBid и т.д.
    /// </summary>
    //public Action  <MarketDepth> DepthUpdated;
    
    public event PropertyChangedEventHandler PropertyChanged;
    
    /// <summary>
    /// the trading status of this instrument on the stock exchange
    /// состояние торгов этим инструментом на бирже
    /// </summary>
    public SecurityStateType State;

    /// <summary>
    /// price step, i.e. minimal price change for the instrument
    /// шаг цены, т.е. минимальное изменение цены для инструмента
    /// </summary>
    public required decimal PriceStep
    {
        get { return _priceStep; }
        set
        {
            _priceStep = value;

            if (_priceStep >= 1 ||
                _priceStep == 0)
            {
                _decimals = 0;
                return;
            }

            string step = Convert.ToDecimal(Convert.ToDouble(_priceStep)).ToString(new CultureInfo("ru-RU"));
           
            if(step.Split(',').Length == 1)
            {
                _decimals = 0;
            }
            else
            {
                _decimals = step.Split(',')[1].Length;
            }
        }
    }

    private decimal _priceStep;

    /// <summary>
    /// the cost of a step of the price, i.e. how much profit is dripping on the deposit for one step of the price
    /// стоимость шага цены, т.е. сколько профита капает на депозит за один шаг цены
    /// </summary>
    public required decimal PriceStepCost;

    /// <summary>
    /// warranty coverage
    /// гарантийное обеспечение
    /// </summary>
    public decimal Go;

    /// <summary>
    /// open the Paper Settings window
    /// вызвать окно настроек бумаги
    /// </summary>
  

    /// <summary>
    /// the number of decimal places of the instrument price.
    /// if the price step is higher, or the raver 1, for example 10, then it still returns 0
    /// количество знаков после запятой цены инструмента.
    /// если шаг цены больше, либо равер 1, например 10, то возвращается всё равно 0
    /// </summary>
    public int Decimals
    {
        get
        {
            if (_decimals != 0)
            {
                return _decimals;
            }

            if (PriceStep >= 1 ||
                PriceStep == 0)
            {
                return 0;
            }


            string step = Convert.ToDecimal(Convert.ToDouble(PriceStep)).ToString(new CultureInfo("ru-RU"));

            if (step.Split(',').Length > 1)
            {
                _decimals = step.Split(',')[1].Length;
            }
            else if(step.Split('.').Length > 1)
            {
                _decimals = step.Split('.')[1].Length;
            }

            return _decimals;

        }
        set
        {
            if (value >= 0)
            {
                _decimals = value;
            }
        }
    }
    private int _decimals = -1;

    /// <summary>
    /// the number of decimal places of the instrument volume
    /// количество знаков после запятой объёма инструмента
    /// </summary>
    public int DecimalsVolume;

    /// <summary>
    /// минимальный объём торгов по активу
    /// </summary>
    public decimal MinTradeAmount = 1;

    /// <summary>
    /// Lower price limit for bids. If you place an order with a price lower - the system will reject
    /// Нижний лимит цены для заявок. Если выставить ордер с ценой ниже - система отвергнет
    /// </summary>
    public required decimal PriceLimitLow;

    /// <summary>
    /// Upper price limit for bids. If you place an order with a price higher - the system will reject
    /// Верхний лимит цены для заявок. Если выставить ордер с ценой выше - система отвергнет
    /// </summary>
    public required decimal PriceLimitHigh;
    // For options
    // для опционов

    /// <summary>
    /// option type
    /// тип опциона
    /// </summary>
    public OptionType OptionType;

    /// <summary>
    /// strike
    /// страйк
    /// </summary>
    public decimal Strike;

    /// <summary>
    /// expiration date
    /// дата экспирации
    /// </summary>
    public DateTime Expiration;
    // save and load
    // сохранение и загрузка



    public void PropertyEvent(string _property)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(_property));
    }

}

/// <summary>
/// stock market conditions
/// состояние бумаги на бирже
/// </summary>
public enum SecurityStateType
{
    /// <summary>
    /// trading on the paper is active
    /// торги по бумаге активны
    /// </summary>
    Activ,

    /// <summary>
    /// paper auction is closed.
    /// торги по бумаге закрыты
    /// </summary>
    Close,

    /// <summary>
    /// we don't know if the bidding's going on
    /// неизвестно, идут ли торги
    /// </summary>
    UnKnown
}

/// <summary>
/// instrumental type
/// тип инструмента
/// </summary>
public enum SecurityType
{
    /// <summary>
    /// none
    /// не определено
    /// </summary>
    None,

    /// <summary>
    /// currency. Including crypt
    /// валюта. В т.ч. и крипта
    /// </summary>
    CurrencyPair,

    /// <summary>
    /// акция
    /// </summary>
    Stock,

    /// <summary>
    /// облигация
    /// </summary>
    Bond,

    /// <summary>
    /// futures
    /// фьючерс
    /// </summary>
    Futures,

    /// <summary>
    /// option
    /// опцион
    /// </summary>
    Option,

    /// <summary>
    /// index индекс
    /// </summary>
    Index
}

/// <summary>
/// option type
/// тип опциона
/// </summary>
public enum OptionType
{
    /// <summary>
    /// none
    /// не определено
    /// </summary>
    None,
    /// <summary>
    /// put 
    /// пут
    /// </summary>
    Put,

    /// <summary>
    /// call
    /// колл
    /// </summary>
    Call
}

