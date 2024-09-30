
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PlazaEngine.Engine;
using PlazaEngine.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;



/*
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/getmessage", () => "Hello Artem");
app.Run();*/


ConcurrentDictionary<string, Security> Securities = new ConcurrentDictionary<string, Security>();
PlazaConnector plaza;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });


builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton(_ =>
{
    
     plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ", test: false, appname: "osaApplication")
    {
        Limit = 30,
        LoadTicksFromStart = false,
    };


    plaza.UpdateSecurity += sec =>
    {
        Securities[sec.Id] = sec;
    };
    plaza.Connect();
    return plaza;

});

var app = builder.Build();

app.Services.GetRequiredService<PlazaConnector>();

app.UseCors(_ => _.SetIsOriginAllowed(_ => true)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/Securities", ()=>
{
    return Results.Json(Securities);
});

app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

app.Run();
