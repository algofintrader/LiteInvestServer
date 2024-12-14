using BlazorRenderAuto.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddTelerikBlazor();

builder.Services.AddDevExpressBlazor(options =>
{
	options.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5;
	options.SizeMode = DevExpress.Blazor.SizeMode.Medium;
});

builder.Services.AddScoped<ApiDataService>();
builder.Services.AddScoped<ApiDataServiceTest>();
builder.Services.AddScoped<JsInteropService>();

await builder.Build().RunAsync();
