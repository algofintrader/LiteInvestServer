
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
ConcurrentDictionary<string, List<IWebSocketConnection>> MyOrderSubscruptions = new ConcurrentDictionary<string, List<IWebSocketConnection>>();

//подписка ордер бук
//ConcurrentDictionary<string, User> MyOrderSubscruptions;

PlazaConnector plaza = null;

string adress = "ws://0.0.0.0:8181/";
var server = new WebSocketServer(adress);
server.RestartAfterListenError = true;
server.ListenerSocket.NoDelay = true;

var wsConenctions = new List<IWebSocketConnection>();


Dictionary<string,string> GetParameters(IWebSocketConnection websocket)
{
    Uri myUri = new Uri(adress + websocket.ConnectionInfo.Path);

    Dictionary<string, string> parameters = new Dictionary<string, string>();
    var resultParameters = HttpUtility.ParseQueryString(myUri.Query);
    foreach (string item in resultParameters.Keys)
    {
        parameters.Add(item, resultParameters.Get(item));
    };

    return parameters;
}



server.Start(ws =>
{


    ws.OnOpen = () =>
    {
        var parameters = GetParameters(ws);

        if (parameters.ContainsKey("stream") && parameters["stream"] == "myorders")
        {
            if (!parameters.ContainsKey("user"))
            {
                ws.Close();
                return;
            }

            var username = parameters["user"];

            if (!MyOrderSubscruptions.ContainsKey(username))
            {
                MyOrderSubscruptions[username] = new List<IWebSocketConnection>();
            }

            MyOrderSubscruptions[username].Add(ws);
            Console.WriteLine("WebSocket Opened");
        }
    };


    //TODO: переписать либу, чтобы она кидала сам сокет
    //иначе это все может перемешаться в кашу 
    ws.OnClose = () =>
    {
        var parameters = GetParameters(ws);
        var res = MyOrderSubscruptions.TryRemove(parameters["user"], out var _);

        Console.WriteLine("WebSocket Closed " + res);
    };
   
});


var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton(_ =>
{

    plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ", test: false, appname: "osaApplication")
    {
        Limit = 30,
        LoadTicksFromStart = false,
    };

    plaza.OrderChangedEvent += async (plazaOrder, reason) =>
    {

        var username =plazaOrder.Comment;

        if (!Users.ContainsKey(username))
            return;

        //далее по подпискам на сокеты мы должны отправить инфу о новом состоянии юзера..

    };


    plaza.MarketDepthChangeEvent += orderbook =>
    {

        Console.WriteLine(orderbook.SecurityId + " " + orderbook.Bids.FirstOrDefault().Price);
    };

    plaza.UpdateSecurity += sec =>
    {
        Securities[sec.Id] = sec;
    };
    //plaza.Connect();
    return plaza;

});

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


app.MapPost("/NewOrder", async (string userName,ClientOrder clientOrder) =>
{

    try
    {
        if (!Users.ContainsKey(userName))
            return Results.Problem("User not found");
        //проверка, а есть ли такой юзер и может ли он торговать

        var user = Users[userName];

        if (!user.CanTrade)
            return Results.Problem("User Can not trade!");

        if (!plaza.Securities.ContainsKey(clientOrder.Security))
            return Results.Problem("No Security Id Found");

        //Проверка цены?Todo

        //по мне так ужасный код, лучше бы свойства оставили, вместо дублирующего конструктора. 
        //В итоге еще и в самом Ордере кошмар 

        Order plazaOrder = clientOrder.Market ?
           new Order(plaza.Securities[clientOrder.Security], clientOrder.Side, clientOrder.Volume, plaza.Portfolio.Number, userName) :
            new Order(plaza.Securities[clientOrder.Security], clientOrder.Side, clientOrder.Volume, (decimal)clientOrder.Price, plaza.Portfolio.Number, userName);

        Console.Out.WriteAsync($"Sending Order {userName} price={plazaOrder.PriceOrder}");

        await plaza.ExecuteOrder(plazaOrder);

        return Results.Accepted<ClientOrder>();
    }
    catch (Exception ex)
    {  
        return Results.Problem(ex.Message); 
    }

});

app.MapGet("/GetAllSecurities", async () =>
{
    if (Securities == null || Securities.Count == 0)
        return Results.Problem("No Security");

    return Results.Json(Securities);
});

app.MapGet("/SecuritySubscribe", (string secKEY) =>
{

    var sec = Securities[secKEY];
    plaza.RegisterMarketDepth(sec, false);

    return StatusCodes.Status200OK;
});



app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

app.Run();


