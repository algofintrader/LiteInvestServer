using LiteInvestMainFront.Components;
using LiteInvestMainFront.Data;
using LiteInvestMainFront.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDevExpressBlazor(config => config.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5);
builder.Services.AddBlazorBootstrap();

builder.Services.AddMvc();

builder.Services.AddScoped<ApiDataService>();
builder.Services.AddScoped<JsInteropService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
