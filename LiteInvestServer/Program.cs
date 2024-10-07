
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PlazaEngine.Engine;
using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

using Fleck;
using LiteInvestServer.Entity;

using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Annotations;
using LiteInvestServer.Records;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using System;
using Amazon.Runtime.Internal.Util;
using System.Web;
using Amazon.Runtime.Internal.Transform;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;
using LiteInvestServer.Helpers;
using System.Text.Json;
using System.Runtime.CompilerServices;
using LiteInvestServer.Json;
using LiteInvestServer.WebScoketFactory;
using System.Net.Sockets;



//NOTE: Идея такая короче. Протестировать за неделю, все что связано с торговлей
// На выходных сделать админские моменты

//TODO: Протестировать сценарий выставления заявки
//и Чтобы ответ пришел по сокетам
// TODO: Провести рефакторинг сокетов. 
// TODO: Убрать юзера напрямую. 


//NOTE: Скорее всего у нас постоянно будет переподключение, поэтому мы должны сами обновлять постоянно инструменты

/*
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/getmessage", () => "Hello Artem");
app.Run();*/


ConcurrentDictionary<string, Security> Securities = new ConcurrentDictionary<string, Security>();

///База юзеров 
ConcurrentDictionary<string, User> Users = new ConcurrentDictionary<string, User>();

//подписка мои ордера
//возможно один юзер захочет несколько раз хлопнуться )
//ключ = название юзера
//ConcurrentDictionary<string, ConcurrentDictionary<int, IWebSocketConnection>> MyOrdersSockets = new();

//ключ secid 
//нахера нам вообще юзер здесь сдался
//ConcurrentDictionary<string, ConcurrentDictionary<int, IWebSocketConnection>> AllTicksSockets = new();

//подписка ордер бук
//ConcurrentDictionary<string, User> MyOrderSubscruptions;

PlazaConnector plaza = null;
WebSocketEngine webSocketEngine = null;

//TODO: Вынести настройки отдельно потом
string webscoketAdress = "ws://0.0.0.0:8181/";
string userdBdName = "Data/users.xml";

//websocket
string myordersWebSocketstreamName = "my_orders";
string publicTradesSocketstreamName = "public_trades";

var serializer = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };

if (File.Exists(userdBdName))
{
    Users = Helper.ReadXml<ConcurrentDictionary<string, User>>(userdBdName);
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

bool CheckConditionsForWebsocket(WebSocketEngine webSocketEngine,string name )
{
    if (webSocketEngine == null)
        return false;

    var websockets = webSocketEngine.GetSocketsFull(name);

    if (websockets == null)
        return false;

    return true;
}

///PLAZA WORDER MAIN
builder.Services.AddSingleton(_ =>
{

    plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ", test: false, appname: "osaApplication")
    {
        Limit = 30,
        LoadTicksFromStart = false,
    };

    plaza.TicksLoadedEvent += () =>
    {
        Console.WriteLine($"Ticks Ready To Go!");

    };

    plaza.NewTickCollectionEvent += ticksDictionary =>
    {
        //NOTE: Проще проверить все тики
        //Или из подписки найти обновленные тики. 
        //вопрос.. блять

        //-----------------------------
        //TODO: Рефакторить!


        foreach (var tick in ticksDictionary)
            LogMessageAsync($"tiks arrive {tick.Key} count = {tick.Value.Count()}");

        if (webSocketEngine == null)
            return;

        var wesbokcetsKEYS = webSocketEngine.GetSocketsFull(publicTradesSocketstreamName).Values;
        //это список сокетов, где ключ - это хеш сокетаю
        var websockets = webSocketEngine.GetSockets(publicTradesSocketstreamName, "sec_id");
        //Получаем список где ключ это инструмента а значение это сокет

        if (websockets == null) 
            return;

        //-----------------------------

      

        //проверяем всех наших подписантов
        foreach (var secIdsubcription in wesbokcetsKEYS)
        {
            LogMessageAsync($"sec {secIdsubcription}");
            //в тиках есть тики, которые мы должны отправить
            
            /*
            if (ticksDictionary.ContainsKey(secIdsubcription) && ticksDictionary[secIdsubcription.Key].Count!=0)
            {
                var serializedOrder = JsonSerializer.Serialize(ticksDictionary[secIdsubcription.Key]);
              
                
                //собственно отправляем их 
                foreach (var socket in secIdsubcription.Value)
                {
                    LogMessageAsync($"{socket.Key} Sending pack of ticks");
                    socket.Value.Send(serializedOrder);
                }
            }*/
        }

    };

    plaza.OrderLoadedEvent += () =>
    {
        LogMessageAsync($"Orders Loaded!");
    };

    plaza.OrderChangedEvent += async (plazaOrder, reason) =>
    {
        //-----------------------------
        //TODO: Рефакторить!
        if (webSocketEngine == null)
            return;


        //-----------------------------

        var username = plazaOrder.Comment;

        if (!Users.ContainsKey(username) || username != string.Empty)
            return;

        //-----------
        var websockets = webSocketEngine.GetSockets(myordersWebSocketstreamName, username);

        if (websockets == null)
            return;


        Console.WriteLine($"New Order user ({username}) {plazaOrder.State} number = {plazaOrder.ExchangeOrderId}");

        try
        {
            /*
            foreach (var connection in websockets)
            {
                var serializedOrder = JsonSerializer.Serialize(plazaOrder, serializer);
                await connection.Send(serializedOrder).ConfigureAwait(false);
            }*/
        }
        catch (Exception ex)
        {
            LogMessageAsync(ex.Message);
        }

        //далее по подпискам на сокеты мы должны отправить инфу о новом состоянии юзера..
    };

    plaza.MarketDepthChangeEvent += orderbook =>
    {

        Console.WriteLine(orderbook.SecurityId + " " + orderbook.Bids.FirstOrDefault().Price);
    };


    //TODO: Выяснить что именно и как использовать и как обновлять эту бд. 
    
    plaza.UpdateSecurity += sec =>
    {
        Securities[sec.Id] = sec;
    };
    plaza.Connect();
    return plaza;

});

builder.Services.AddSingleton(_ =>
{
    webSocketEngine = new WebSocketEngine(webscoketAdress);

    webSocketEngine.AddStream(myordersWebSocketstreamName, "My Orders", new List<ParameterKey>()
{
    new ParameterKey("user", ParameterTypes.Key)
});

    webSocketEngine.AddStream(publicTradesSocketstreamName, "Ticks", new List<ParameterKey>()
{
    new ParameterKey("sec_id", ParameterTypes.Key),
}, plaza.Register_Unregister_Ticks);

    webSocketEngine.Start();
    return webSocketEngine;
});

var app = builder.Build();

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
   
RiskManager.MapPost("/CreateUser", async (UserCredentials userCredentials,string token ) =>
{
    //TODO: проверка токена 

    if(token == null || token == string.Empty)
        return Results.Problem("Token empty");

    if (Users.ContainsKey(userCredentials.Login))
    {
        return Results.Problem("User with such name already created");
    }

    //TODO: проверка на создание лимита?


    try
    {
        Users.TryAdd(userCredentials.Login, new User(userCredentials.Login, userCredentials.Password)
        { Limit = 20000, CanTrade = true });

        //NOTE: пароли как бы надо получать в зашифрованном виде скорее всего. 
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

    return Results.Accepted<UserCredentials>();
}).WithDescription("Юзер изначально создается с лимитом в 20 000 р. и с возможностью торговать. Предполагается, что потом зайдут в админку и поменяют лимиты");

RiskManager.MapPost("/ChangeLimitUser", async (UserCredentials userCredentials, string token,decimal newlimit) =>
{ 
    return Results.Accepted<UserCredentials>(); 
}).WithDescription("Изменение лимита Юзера");

RiskManager.MapPost("/CanTrade", async (UserCredentials userCredentials, string token,string cantrade) =>
{
    return Results.Accepted<UserCredentials>();
}).WithDescription("Возможность торговать");

var FuturesApi = app.MapGroup("/FuturesApi").WithTags("FuturesApi")
    .WithTags("FuturesApi")
    .WithDescription("Торговля Фьючерсами. Выставление, Отмена заявок");

FuturesApi.MapPost("/NewOrder", async (string userName,ClientOrder clientOrder) =>
{

    try
    {
        if (!Users.ContainsKey(userName))
            return Results.Problem("User not found");
        //проверка, а есть ли такой юзер и может ли он торговать

        var user = Users[userName];

        if (!user.CanTrade)
            return Results.Problem("User Can not trade!");

        if (!plaza.Securities.ContainsKey(clientOrder.SecID))
            return Results.Problem("No Security Id Found");

        //Проверка цены?Todo

        //по мне так ужасный код, лучше бы свойства оставили, вместо дублирующего конструктора. 
        //В итоге еще и в самом Ордере кошмар 

        Order plazaOrder = clientOrder.Market ?
           new Order(plaza.Securities[clientOrder.SecID], clientOrder.Side, clientOrder.Volume, plaza.Portfolio.Number, userName) :
            new Order(plaza.Securities[clientOrder.SecID], clientOrder.Side, clientOrder.Volume, (decimal)clientOrder.Price, plaza.Portfolio.Number, userName);

        LogMessageAsync($"Sending Order {userName} price={plazaOrder.PriceOrder} ");

        await plaza.ExecuteOrder(plazaOrder);

        return Results.Accepted<ClientOrder>();
    }
    catch (Exception ex)
    {  
        return Results.Problem(ex.Message); 
    }

});

FuturesApi.MapGet("/GetAllSecurities", async () =>
{
    if (Securities == null || Securities.Count == 0)
        return Results.Problem("No Security");

    //пришлось такой брут перевод сделать,
    //чтобы наш объект сервера не зависел от объекта у плазы

    Dictionary<string, SecurityApi> securitiesJson = new();
    foreach (var sec in Securities.Values)
    {
        securitiesJson.Add(sec.Id, new SecurityApi()
        {
            id = sec.Id,
            ShortName = sec.ShortName,
            ClassCode = sec.ClassCode,
            FullName = sec.FullName,
            Type = sec.Type.ToString(),
            Lot= sec.Lot,
            PriceStep = sec.PriceStep,
            Decimals = sec.Decimals,
            PriceLimitHigh = sec.PriceLimitHigh,
            PriceLimitLow =  sec.PriceLimitLow,
        });
    }
    return Results.Json(securitiesJson);
});

/*
FuturesApi.MapGet("/SubscribeSecurityTicks", (string secKEY) =>
{

    var sec = Securities[secKEY];
    plaza.RegisterTicks(sec);

    return StatusCodes.Status200OK;
}).WithDescription("Подписка на обезличенные тики");

FuturesApi.MapGet("/SubscribeSecurityQuotes", (string secKEY) =>
{

    var sec = Securities[secKEY];
    plaza.RegisterMarketDepth(sec, false);

    return StatusCodes.Status200OK;
});*/



app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

async void LogMessageAsync(string message)
{
    await Console.Out.WriteLineAsync(message).ConfigureAwait(false);
}

//TODO: Переделать сохранение в промежутках по человечески
AppDomain.CurrentDomain.ProcessExit += (_,_) =>
{
    try
    {
        Helper.SaveXml(Users, userdBdName);

        if (plaza != null)
            plaza.Dispose();
    }
    catch (Exception ex)
    {
        
    }
};

app.Run();

