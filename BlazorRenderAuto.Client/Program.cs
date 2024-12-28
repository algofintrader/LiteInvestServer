using BlazorRenderAuto.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddTelerikBlazor();

//builder.Services.AddBlazorBootstrap();
builder.Services.AddSingleton<ApiDataService>();

//builder.Services.AddScoped<ApiDataServiceTest>();

//TODO
builder.Services.AddScoped<JsInteropService>();

await builder.Build().RunAsync();
