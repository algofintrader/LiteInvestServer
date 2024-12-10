using BlazorRenderAuto.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddTelerikBlazor();
builder.Services.AddDevExpressBlazor(config => config.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5);



builder.Services.AddScoped<ApiDataService>();
builder.Services.AddScoped<ApiDataServiceTest>();

await builder.Build().RunAsync();
