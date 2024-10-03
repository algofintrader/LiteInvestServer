﻿
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
ConcurrentDictionary<string, ConcurrentDictionary<int, IWebSocketConnection>> MyOrdersSockets = new();

ConcurrentDictionary<string, ConcurrentDictionary<int, IWebSocketConnection>> AllTicksSockets = new();

//подписка ордер бук
//ConcurrentDictionary<string, User> MyOrderSubscruptions;

PlazaConnector plaza = null;

string webscoketAdress = "ws://0.0.0.0:8181/";
string userdBdName = "Data/users.xml";

var serializer = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
var server = new WebSocketServer(webscoketAdress);
server.RestartAfterListenError = true;
server.ListenerSocket.NoDelay = true;

var wsConenctions = new List<IWebSocketConnection>();

if (File.Exists(userdBdName))
{
    Users = Helper.ReadXml<ConcurrentDictionary<string, User>>(userdBdName);
}


Dictionary<string,string> GetParameters(IWebSocketConnection websocket)
{
    Uri myUri = new Uri(webscoketAdress + websocket.ConnectionInfo.Path);

    Dictionary<string, string> parameters = new Dictionary<string, string>();
    var resultParameters = HttpUtility.ParseQueryString(myUri.Query);
    foreach (string item in resultParameters.Keys)
    {
        parameters.Add(item, resultParameters.Get(item));
    };

    return parameters;
}


//TODO: Добавить ключ авторизации, который скорее всего идет уже при авторизации и так и так



server.Start(async ws =>
{
    var parameters = GetParameters(ws);

    if (parameters.ContainsKey("stream"))
    {
        Console.WriteLine($"No stream data for WS");
        ws.Close();
        return;
    }

    if (!parameters.ContainsKey("user"))
    {
        Console.WriteLine($"No UserName");
        ws.Close();
        return;
    }

    var username = parameters["user"];
    var streamname = parameters["stream"];

    //TODO: Весь этот код отрефакторить и перевести в некую фабрику сокетов собственно говоря. 

    if (streamname == "my_orders")
    {
        if (!MyOrdersSockets.ContainsKey(username))
            MyOrdersSockets[username] = new();

        //NOTE: Может ли GetHash в рамках одного юзера повторится?
        var hash = ws.GetHashCode();
        MyOrdersSockets[username][hash] = ws;
        Console.WriteLine($"WebSocket for Orders Opened {username} stream = {streamname} hash = {hash}");
    }
    else if(streamname == "public_trades")
    {
        if (!AllTicksSockets.ContainsKey(username))
            AllTicksSockets[username] = new();

        var hash = ws.GetHashCode();
        AllTicksSockets[username][hash] = ws;
        Console.WriteLine($"WebSocket for Ticks Opened {username} stream = {streamname} hash = {hash}");
    }
    else
    {
        Console.WriteLine($"No Correct parameters for subscribing");
        ws.Close();
    }

    /*
    ws.OnOpen = () =>
    {

    };
    */

    ws.OnClose = async (closingwebsocket) =>
    {
        var hash = closingwebsocket.GetHashCode();
        var parameters = GetParameters(closingwebsocket);
        var streamname = parameters["stream"];

        try
        {
            if (streamname == "my_orders")
                MyOrdersSockets[parameters["user"]].TryRemove(hash, out _);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Webscoket delete Error: {ex.Message}");
        }

        Console.WriteLine($"WebSocket Removed hash = {hash}");
    };

});


var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



///PLAZA WORDER MAIN
builder.Services.AddSingleton(_ =>
{

    plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ", test: false, appname: "osaApplication")
    {
        Limit = 30,
        LoadTicksFromStart = false,
    };

    plaza.OrderLoadedEvent += () =>
    {
        Console.WriteLine($"Orders Loaded!");
    };

    plaza.OrderChangedEvent += async (plazaOrder, reason) =>
    {

        var username =plazaOrder.Comment;

        if (!Users.ContainsKey(username))
            return;

        Console.WriteLine($"New Order user ({username}) {plazaOrder.State} number = {plazaOrder.ExchangeOrderId}");

        if(MyOrdersSockets.ContainsKey(username) && MyOrdersSockets[username].Count!=0)
        {
            foreach(var connection in MyOrdersSockets[username])
            {
                
                var serializedOrder = JsonSerializer.Serialize(plazaOrder, serializer);
                connection.Value.Send(serializedOrder);
            }
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

void Plaza_OrderLoadedEvent()
{
    throw new NotImplementedException();
}

var app = builder.Build();

app.Services.GetRequiredService<PlazaConnector>();

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

        Console.Out.WriteAsync($"Sending Order {userName} price={plazaOrder.PriceOrder} ");

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

FuturesApi.MapGet("/SecuritySubscribeTicks", (string secKEY) =>
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
});



app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();



//TODO: Переделать сохранение в промежутках по человечески
AppDomain.CurrentDomain.ProcessExit += (_,_) =>
{

    Helper.SaveXml(Users, userdBdName);

    if(plaza!=null)
        plaza.Dispose();
};

app.Run();

