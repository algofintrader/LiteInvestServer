using MongoDB.Driver;
using PlazaEngine.Engine;
using PlazaEngine.Entity;
using System.Collections.Concurrent;
using LiteInvestServer.Entity;
using LiteInvestServer.Records;
using LiteInvestServer.Helpers;
using System.Text.Json;
using LiteInvestServer.Json;
using LiteInvestServer.WebScoketFactory;
using LiteInvestServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MongoDB.Bson.Serialization.Serializers;



//NOTE: Идея такая короче. Протестировать за неделю, все что связано с торговлей
// На выходных сделать админские моменты

//NOTE: Скорее всего у нас постоянно будет переподключение, поэтому мы должны сами обновлять постоянно инструменты

ConcurrentDictionary<string, SecurityApi> Securities = new ();
///База юзеров 
ConcurrentDictionary<string, User> UsersContext = new ConcurrentDictionary<string, User>();
//База ордеров по юзеру
ConcurrentDictionary<string, ConcurrentDictionary<string,Order>> Orders = new();
//База ордеров по юзеру
ConcurrentDictionary<string, ConcurrentDictionary<string, Trade>> Trades = new();
//ключ - юзер
//ключ 2 - sec ID
ConcurrentDictionary<string, ConcurrentDictionary<string, Pos>> OpenedPositions = new();
ConcurrentDictionary<string, ConcurrentDictionary<string, List<Pos>>> ClosedPositions = new();

//это словарь наоборот
// ключ - sec_id
// далее по юзерам идет разбивка. 
ConcurrentDictionary<string, ConcurrentDictionary<string, Pos>> PositionsForProfit = new ();


ConcurrentDictionary<string, PositionOnBoard> RealPositions = new();

PlazaConnector plaza = null;
WebSocketEngine webSocketEngine = null;

object savelocker = new object();

string data = "C:\\ServerData";
var directoryInfo = new DirectoryInfo(data);

//NOTE: Вбил вручную в код, почему то не находит Development settings. 
string webscoketAdress= "ws://0.0.0.0:5000/";

string securitiesBdName = $"{data}\\securities.xml";
string userdBdName = $"{data}\\users.xml";
string ordersBdName = $"{data}\\orders.xml";
string tradesBdName = $"{data}\\trades.xml";

string myordersWebSocketstreamName = "my_orders";
string mytradesWebSocketstreamName = "my_trades";
string publicTradesSocketstreamName = "public_trades";
string orderbookWebSocketstreamName = "orderbook";

string authkeyname = "liteinvest";

var serializer = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };

var wb = new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory };

var builder = WebApplication.CreateBuilder(args);
//var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

var services = builder.Services;
var configuration = builder.Configuration;

var jwtOptions = builder.Configuration.GetSection("JwtOptions").Get<JwtOptions>();
var jwtprovider = new JwtProvider(jwtOptions);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


///PLAZA WORDER MAIN
builder.Services.AddSingleton(_ =>
{

    /* // Тест сохранения 
    Orders.TryAdd("1", new ConcurrentDictionary<string, Order>());
    Orders["1"] = new ConcurrentDictionary<string, Order>();
    Orders["1"]["2"] = new Order();

    try
    {
        Helper.SaveXml(Orders, ordersBdName);
    }
    catch (Exception ex)
    {

    }*/


if (!Directory.Exists(data))
        System.IO.Directory.CreateDirectory(directoryInfo.ToString());


    //TODO: Перепишу всю эту часть, когда сделаю нормальное сохранение. 

    Securities = Helper.ReadXml<ConcurrentDictionary<string, SecurityApi>>(securitiesBdName);
    UsersContext = Helper.ReadXml<ConcurrentDictionary<string, User>>(userdBdName);
    Orders = Helper.ReadXml<ConcurrentDictionary<string, ConcurrentDictionary<string, Order>>>(ordersBdName);
    Trades = Helper.ReadXml<ConcurrentDictionary<string, ConcurrentDictionary<string, Trade>>>(tradesBdName);
    OpenedPositions = Helper.ReadXml<ConcurrentDictionary<string, ConcurrentDictionary<string, Pos>>>($"{data}\\{nameof (OpenedPositions)}.xml");
    ClosedPositions = Helper.ReadXml<ConcurrentDictionary<string, ConcurrentDictionary<string, List<Pos>>>>($"{data}\\{nameof(ClosedPositions)}.xml");

    string admin = "adminadminov";

    if (!UsersContext.ContainsKey(admin))
        UsersContext.TryAdd(admin, new User(admin, "adminPass#1R") { Admin = true, CanTrade = false });

    plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ",Helper.isSimulation(), testTrading: false, appname: "osaApplication")
    {
        Limit = 30,
        LoadTicksFromStart = false,
    };

    plaza.UpdatePosition += pos =>
    {
        RealPositions[pos.SecurityId] = pos;
        LogMessageAsync($"New Pos Info sec_id={pos.SecurityId} {pos.XPosValueCurrent} ");
    };

   plaza.TicksLoadedEvent += () =>
    {
        LogMessageAsync($"Ticks Ready To Go!");
    };

    plaza.NewTickCollectionEvent += ticksDictionary =>
    {
        //NOTE: Проще проверить все тики
        //Или из подписки найти обновленные тики. 
        //вопрос.. блять

        //-----------------------------
        //TODO: Рефакторить!

        if (ticksDictionary == null)
            return;

        
        foreach (var tick in ticksDictionary)
        {
            if(tick.Value.Count!=0)
            LogMessageAsync($"tiks arrive {tick.Key} count = {tick.Value.Count()} priceFirst = {tick.Value.First().Price}");
        }

        try
        {

            if (webSocketEngine == null)
                return;

            var wesbokcets = webSocketEngine.GetSocketsFull(publicTradesSocketstreamName);
            //это список сокетов, где ключ - это хеш сокетаю

            //Получаем список где ключ это инструмента а значение это сокет

            if (wesbokcets == null)
                return;

            //-----------------------------

            //проверяем всех наших подписантов
            foreach (var secIdsubcription in wesbokcets)
            {
                LogMessageAsync($"sec {secIdsubcription.Key}");
                //в тиках есть тики, которые мы должны отправить


                if (ticksDictionary.ContainsKey(secIdsubcription.Key) && ticksDictionary[secIdsubcription.Key].Count != 0)
                {
                    var serializedOrder = JsonSerializer.Serialize(ticksDictionary[secIdsubcription.Key]);

                    //собственно отправляем их 
                    foreach (var socket in secIdsubcription.Value)
                    {
                        LogMessageAsync($"{socket.Key} Sending pack of ticks");
                        socket.Value.Send(serializedOrder);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogMessageAsync($"Problems with websocket {ex.Message}");
        }

    };

    plaza.NewMyTradeEvent += ProcessNewMyTrade;
    plaza.OrderLoadedEvent += () =>
    {
        LogMessageAsync($"Orders Loaded!");
    };

    plaza.OrderChangedEvent += async (plazaOrder, reason) =>
    {

        if (plazaOrder == null)
            return;

        var username = plazaOrder.Comment;

        try
        {
            if (!UsersContext.ContainsKey(username) || username == string.Empty)
                return;

            if (!Orders.ContainsKey(username))
                Orders.TryAdd(username, new());

            Orders[username][plazaOrder.ExchangeOrderId] = plazaOrder;
            LogMessageAsync($"Order add to DB {plazaOrder}");
        }
        catch (Exception ex)
        {
            LogMessageAsync($"Problems adding order {ex.Message}") ;
        }

        //-----------------------------
        //TODO: Рефакторить!
        if (webSocketEngine == null)
            return;
        var websockets = webSocketEngine.GetSockets(myordersWebSocketstreamName, username);
        if (websockets == null)
            return;

        LogMessageAsync($"New Order user ({username}) {plazaOrder.State} number = {plazaOrder.ExchangeOrderId} error ={reason}");

        try
        {
            
            foreach (var connection in websockets)
            {
                plazaOrder.Error = reason;
                var serializedOrder = JsonSerializer.Serialize(plazaOrder, serializer);
                await connection.Value.Send(serializedOrder).ConfigureAwait(false);
                LogMessageAsync($"socket {connection.GetHashCode()} send info ({username}) {plazaOrder.State} number = {plazaOrder.ExchangeOrderId}");
            }
        }
        catch (Exception ex)
        {
            LogMessageAsync(ex.Message);
        }

        //далее по подпискам на сокеты мы должны отправить инфу о новом состоянии юзера..
    };

    plaza.MarketDepthChangeEvent += orderbook =>
    {

        if (orderbook == null)
            return;

        try
        {

            var sec_id = orderbook.SecurityId;
            //-----------
            var websockets = webSocketEngine.GetSockets(orderbookWebSocketstreamName, sec_id);

            if (websockets == null)
                return;

            foreach (var websocket in websockets)
            {
                var serialized = JsonSerializer.Serialize(orderbook, serializer);
                websocket.Value.Send(serialized);
            }

           // if (orderbook.Bids != null && orderbook.Bids.Count != 9)
           //     LogMessageAsync(orderbook.SecurityId + " " + orderbook.Bids[0].Price);
        }
        catch (Exception ex)
        {
            LogMessageAsync(ex.Message);
        }
    };


    Helper.CreateTimerAndStart(CalculatePnls, 5000);
    Helper.CreateTimerAndStart(SaveDb, 5000);

    plaza.UpdateSecurity += sec =>
    {
        Securities[sec.Id] = new SecurityApi()
        {
            id = sec.Id,
            ShortName = sec.ShortName,
            ClassCode = sec.ClassCode,
            FullName = sec.FullName,
            Type = sec.Type.ToString(),
            Lot = sec.Lot,
            PriceStep = sec.PriceStep,
            Decimals = sec.Decimals,
            PriceLimitHigh = sec.PriceLimitHigh,
            PriceLimitLow = sec.PriceLimitLow,
        };
    };
    plaza.Connect();
    return plaza;

});

void CalculatePnls()
{
    try
    {
        if (plaza == null || plaza.ticksplaza == null)
            return;

        foreach (var pos in PositionsForProfit)
        {

            if (!plaza.ticksplaza.AllTicks.ContainsKey(pos.Key))
                continue;

            var tickPrice = plaza.ticksplaza.AllTicks[pos.Key].Price;

            foreach (var userPos in pos.Value)
            {
                userPos.Value.CalculateUnrealizedPnl(tickPrice);
               // LogMessageAsync($"PNL updated for {userPos.Key} sec_id ={pos.Key}");
            }
        }
    }
    catch (Exception ex)
    {
        LogMessageAsync(ex.Message);
    }
}
void ProcessNewMyTrade(MyTrade newMytrade)
{
    try
    {
        var username = newMytrade.Comment;

        if (!UsersContext.ContainsKey(username))
            return;

        if (!OpenedPositions.ContainsKey(username))
            OpenedPositions.TryAdd(username, new ConcurrentDictionary<string, Pos>());

        OpenedPositions[username].TryGetValue(newMytrade.SecurityId, out var posvalue);

        if (posvalue == null)
        {
            OpenedPositions[username][newMytrade.SecurityId] = new Pos();
            LogMessageAsync($"Creating new Pos for {username} ");
        }

        var resultAddingTrade = OpenedPositions[username][newMytrade.SecurityId].AddTrade(newMytrade);
        LogMessageAsync($"Update for {username} sec_id={newMytrade.SecurityId} pos {OpenedPositions[username][newMytrade.SecurityId].PosValue}");

        //-------------------------- Позиции для подсчета прибыли ------------------------//

        PositionsForProfit.TryGetValue(newMytrade.SecurityId, out var valuekeypair);
        if (valuekeypair == null) PositionsForProfit[newMytrade.SecurityId] = new();
        PositionsForProfit[newMytrade.SecurityId][username] = OpenedPositions[username][newMytrade.SecurityId];

        //---------------------------Позиции для подсчета прибыли -----------------------//

        if (resultAddingTrade != null && resultAddingTrade.Closed)
        {
            if (!ClosedPositions.ContainsKey(username))
                ClosedPositions[username] = new();

            var closedpos = OpenedPositions[username][newMytrade.SecurityId];

            if (!ClosedPositions[username].ContainsKey(newMytrade.SecurityId))
                ClosedPositions[username][newMytrade.SecurityId] = new List<Pos>();

            ClosedPositions[username][newMytrade.SecurityId].Add(closedpos);

            if(OpenedPositions[username].TryRemove(newMytrade.SecurityId, out var _))
            {
                LogMessageAsync($"Pos Deleted {username} sec_id={newMytrade.SecurityId} ");
            }

            //удаляем вообще какие либо позы юзера, потому что потом будет
            //по этому словарю будем считать прибыль сами
            if (OpenedPositions[username].Values.Count == 0)
                OpenedPositions.TryRemove(username, out var _);

            if (PositionsForProfit.ContainsKey(newMytrade.SecurityId) && PositionsForProfit[newMytrade.SecurityId].ContainsKey(username))
            {
                PositionsForProfit[newMytrade.SecurityId].TryRemove(username, out var _);

                //удаляем вообще все намеки на остатки... 
                if (PositionsForProfit[newMytrade.SecurityId].Values.Count == 0)
                    PositionsForProfit.TryRemove(newMytrade.SecurityId, out _);
            }
        }

        //добавилось остаточная позиция, как то так... 
        if (resultAddingTrade != null && resultAddingTrade.RestNewTrade != null)
        {
            ProcessNewMyTrade(resultAddingTrade.RestNewTrade);
        }
    }
    catch (Exception ex)
    {
        LogMessageAsync(ex.Message);
    }
}

builder.Services.AddSingleton(_ =>
{
    
    webSocketEngine = new WebSocketEngine(webscoketAdress,authkeyname, jwtOptions,UsersContext);

    webSocketEngine.AddStream(myordersWebSocketstreamName, "My Orders", new List<ParameterKey>()
{
    new ParameterKey(WebSocketKeys.user.ToString(), ParameterTypes.Key)
});

    webSocketEngine.AddStream(publicTradesSocketstreamName, "Ticks", new List<ParameterKey>()
{
    new ParameterKey(WebSocketKeys.sec_id.ToString(), ParameterTypes.Key),
}, plaza.Register_Unregister_Ticks);

    webSocketEngine.AddStream(orderbookWebSocketstreamName, "OrderBook", new List<ParameterKey>()
{
    new ParameterKey(WebSocketKeys.sec_id.ToString(), ParameterTypes.Key),
}, plaza.Register_Unregister_MarketDepth);

    webSocketEngine.Start();
    return webSocketEngine;
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtProvider.GetBasicTokenValidationParameters(jwtOptions);
        options.Events = new JwtBearerEvents 
        {OnMessageReceived = (context)=>
        {
            context.Token = context.Request.Cookies[authkeyname];
            return Task.CompletedTask;
        }};
        services.AddAuthorization();
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    webscoketAdress = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("WebsocketAdress")["Adress"];
   
    /*X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);

    store.Open(OpenFlags.ReadOnly);

    foreach (X509Certificate2 certificate in store.Certificates)
    {
        //TODO's
    }*/
}

//регистрация плазы сервиса
app.Services.GetRequiredService<PlazaConnector>();

//регистрируем веб сокет 
app.Services.GetRequiredService<WebSocketEngine>();

app.UseCors(_ => _.SetIsOriginAllowed(_ => true)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod());


app.UseSwagger();
app.UseSwaggerUI(_ =>
{
    _.EnableTryItOutByDefault();
    _.DisplayRequestDuration();
    _.EnablePersistAuthorization();
});

var RiskManager = app.MapGroup("/RiskManager")
    .WithTags("RiskManager");

   
RiskManager.MapGet("/GetUsers", async (HttpContext httpContext) =>
{

    string username = httpContext.GetUserName();

    if (!UsersContext.ContainsKey(username))
        return Results.Problem("User not found");

    if (!UsersContext[username].Admin)
        return Results.Problem("No Admin rights");
    try
    {
         return Results.Json(UsersContext.ToList().Where(a=>!a.Value.Admin));
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
}).RequireAuthorization().WithDescription("Выдача списка юзеров");


RiskManager.MapPost("/ChangeLimitUser", async (HttpContext httpContext,string usernametochange,decimal newlimit) =>
{
    string username = httpContext.GetUserName();

    if (!UsersContext.ContainsKey(username))
        return Results.Problem("User not found");

    if (!UsersContext[username].Admin)
        return Results.Problem("No Admin rights");
    try
    {
        UsersContext[usernametochange].Limit = newlimit;
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

    return Results.Accepted<UserCredentials>(); 
}).WithDescription("Изменение лимита Юзера");

RiskManager.MapPost("/CanTrade", async (HttpContext httpContext, string usernametochange, bool cantrade) =>
{
    string username = httpContext.GetUserName();

    if (!UsersContext.ContainsKey(username))
        return Results.Problem("User not found");

    if (!UsersContext[username].Admin)
        return Results.Problem("No Admin rights");

    if (UsersContext[usernametochange].Admin)
    {
        return Results.Problem("You can not change settings for admin to trade");
    }

    try
    {
        UsersContext[usernametochange].CanTrade = cantrade;
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

    return Results.Accepted<UserCredentials>();
}).WithDescription("Возможность торговать");

RiskManager.MapPost("/CloseAllPositions", async (HttpContext httpContext) =>
{
    string username = httpContext.GetUserName();

    if (!UsersContext.ContainsKey(username))
        return Results.Problem("User not found");

    if (!UsersContext[username].Admin)
        return Results.Problem("No Admin rights");

    if (plaza == null)
        return Results.Problem("Plaza Not Ready");
 
    try
    {
        foreach (var pos in RealPositions.Values)
        {
            var sec = plaza.Securities[pos.SecurityId];
            var order = new Order(sec, pos.XPosValueCurrent > 0 ? Side.Sell : Side.Buy, pos.XPosValueCurrent, plaza.Portfolio.Number, username);
            LogMessageAsync("Sending close orders of positions " + order.ToString());
        }   
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

    return Results.Accepted<UserCredentials>();
}).WithDescription("Закрыть позиции от Админа все");

var common = app.MapGroup("/Common").WithTags("Common");

var Trading = app.MapGroup("/Trading").WithTags("Trading");

common.MapPost("/Register", async (UserCredentials userCredentials) =>
{

    if(UsersContext.ContainsKey(userCredentials.LoginEmail))
    {
        return Results.Problem("User already exists!");
    }

    UsersContext.TryAdd(userCredentials.LoginEmail, new User(userCredentials.LoginEmail, userCredentials.Password)
    {
        Limit = 20000, 
        CanTrade = false,
        Admin = false,
    });

    return Results.Accepted<UserCredentials>();
}).WithDescription("Изначально любой может создать юзера, но он не сможет торговать. Админ позже выставит такую возможность");

common.MapPost("/Login", async(string login, string pass, HttpContext httpContext) =>
{
    if (!UsersContext.ContainsKey(login))
    {
        return Results.Problem("User does not exist!");
    }

    var user = UsersContext[login];

    if (user.Password != pass)
    {
        return Results.Problem("Pass incorrect");
    }
    var authResponse = jwtprovider.GenerateToken(user);
    httpContext.Response.Cookies.Append(authkeyname, authResponse.Token);

    return Results.Json(authResponse);
}).Produces<AuthResponse>();

common.MapPost("/LogOut", async (HttpContext httpContext) =>
{
    try
    {
        string username = httpContext.GetUserName();
        httpContext.Response.Cookies.Delete(authkeyname);
    }
    catch (Exception ex)
    {
        Results.Problem(ex.Message);
    }
    return Results.Ok();
});


Trading.MapPost("/SendOrder", async (ClientOrder clientOrder, HttpContext httpContext) =>
{

    try
    {
        string userName = httpContext.GetUserName();
        
        if (!UsersContext.ContainsKey(userName))
            return Results.Problem("User not found");
        //проверка, а есть ли такой юзер и может ли он торговать

        var user = UsersContext[userName];

        if (!user.CanTrade)
            return Results.Problem("User Can not trade!");

        if (!plaza.Securities.ContainsKey(clientOrder.SecID))
            return Results.Problem("No Security Id Found");

        //TODO: ПРОВЕРКА ЛИМИТА ТОРГОВЛИ, чтобы он мог торговать.

        //по мне так ужасный код, лучше бы свойства оставили, вместо дублирующего конструктора. 
        //В итоге еще и в самом Ордере кошмар 

        var price = (decimal)clientOrder.Price;
        var sec = plaza.Securities[clientOrder.SecID];

        if (!clientOrder.Market && (price > sec.PriceLimitHigh || price < sec.PriceLimitLow))
            return Results.Problem($"Price not in range of PriceLimitHigh = {sec.PriceLimitHigh} or PriceLimitLow = {sec.PriceLimitLow}") ;

        Order plazaOrder = clientOrder.Market ?
           new Order(sec, clientOrder.Side, clientOrder.Volume, plaza.Portfolio.Number, userName) :
            new Order(sec, clientOrder.Side, clientOrder.Volume, price, plaza.Portfolio.Number, userName);

        if (clientOrder.NumberOrderId != null && clientOrder.NumberOrderId !=0)
            plazaOrder.NumberUserOrderId = (int)clientOrder.NumberOrderId;

        LogMessageAsync($"Sending Order {userName} price={plazaOrder.PriceOrder} ");

        await plaza.ExecuteOrderAsync(plazaOrder).ConfigureAwait(false);

        var serializedOrder = JsonSerializer.Serialize(plazaOrder, serializer);

        return Results.Json(plazaOrder);
        //return serializedOrder;
        //return Results.Accepted<Order>();
    }
    catch (Exception ex)
    {  
        return Results.Problem(ex.Message); 
    }

}).RequireAuthorization().WithDescription("NumberOrderId если отправлять Null или 0 в итоге не будет использован и будет сгенерирован системой.");

Trading.MapPost("/GetClosedPositions", async (HttpContext httpContext,string sec_id ="") =>
{
    try
    {
        string userName = httpContext.GetUserName();

        if(!ClosedPositions.ContainsKey(userName))
            return Results.Problem("No Data Found");

        if (sec_id.IsNullOrEmpty())
            return Results.Json(ClosedPositions[userName].Values.ToList());

        if (ClosedPositions.ContainsKey(userName) && ClosedPositions[userName].ContainsKey(sec_id))
            return Results.Json(ClosedPositions[userName][sec_id]);

        return Results.Problem("No Data Found");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

}).RequireAuthorization().WithDescription("");

Trading.MapPost("/GetOpenPositions", async (HttpContext httpContext, string sec_id ="") =>
{
    try
    {
        string userName = httpContext.GetUserName();

        if(!OpenedPositions.ContainsKey(userName))
            return Results.Problem("No Data Found");

        if (sec_id.IsNullOrEmpty())
            return Results.Json(OpenedPositions[userName].Values.ToList());

    if (OpenedPositions.ContainsKey(userName) && OpenedPositions[userName].ContainsKey(sec_id) && OpenedPositions[userName][sec_id] != null)
            return Results.Json(new List<Pos>() { OpenedPositions[userName][sec_id] });

        return Results.Problem("No Data Found");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

}).RequireAuthorization().WithDescription("Выдача всех позиций. Если sec_id = 0, то выдаст просто все открытые позиции юзера.");

Trading.MapGet("/GetOrders", async (HttpContext httpContext) =>
{
    try
    {
        string userName = httpContext.GetUserName();

        if (!Orders.ContainsKey(userName))
            return Results.Problem("User not found in Orders");
        //проверка, а есть ли такой юзер и может ли он торговать

        return Results.Json(Orders[userName].Values);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
}).RequireAuthorization().WithDescription("Выдача ордеров - пока что полный срок. TypeOrder = 0 (limit) 1 (market)");

Trading.MapPost("/CancelOrder", async (HttpContext httpContext, int numberId) =>
{
    try
    {
        string userName = httpContext.GetUserName();

        if (!UsersContext.ContainsKey(userName))
            return Results.Problem("User not found");
        //проверка, а есть ли такой юзер и может ли он торговать

        var user = UsersContext[userName];

        if (!user.CanTrade)
            return Results.Problem("User Can not trade!");

        //TODO: ПРОВЕРКА ЛИМИТА ТОРГОВЛИ, чтобы он мог торговать.

        //по мне так ужасный код, лучше бы свойства оставили, вместо дублирующего конструктора. 
        //В итоге еще и в самом Ордере кошмар 


        await plaza.CancelOrder(numberId);

        return Results.Accepted<ClientOrder>();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
}).RequireAuthorization();

Trading.MapGet("/GetAllSecurities", async () =>
{
    if (Securities == null || Securities.Count == 0)
        return Results.Problem("No Security");
    //пришлось такой брут перевод сделать,
    //чтобы наш объект сервера не зависел от объекта у плазы
    return Results.Json(Securities.Values.OrderBy(s=>s.ShortName));

}).RequireAuthorization();




app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

async void LogMessageAsync(string message)
{
    var dt =DateTime.Now.ToString("H:mm:ss.fff");
    await Console.Out.WriteLineAsync($"{dt} {message}").ConfigureAwait(false);
}


void SaveDb()
{
    lock (savelocker)
    {
        Helper.SaveXml(Securities, securitiesBdName);
        Helper.SaveXml(Trades, tradesBdName);
        Helper.SaveXml(Orders, ordersBdName);
        Helper.SaveXml(UsersContext, userdBdName);

        Helper.SaveXml(OpenedPositions, $"{data}\\{nameof(OpenedPositions)}.xml");
        Helper.SaveXml(ClosedPositions, $"{data}\\{nameof(ClosedPositions)}.xml");
    }
}

//TODO: Переделать сохранение в промежутках по человечески
AppDomain.CurrentDomain.ProcessExit += (_,_) =>
{
    try
    {

        //TODO: переписать этот кошмар... 

        SaveDb();

        if (plaza != null)
            plaza.Dispose();
    }
    catch (Exception ex)
    {
       
    }
};

app.Run();
//Console.ReadLine();

