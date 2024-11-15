using BlazorApp3.Components;
using BlazorApp3.Data;
using Devexpress.Blazor;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


//.AddInteractiveWebAssemblyComponents();


builder.Services.AddDevExpressBlazor(config => config.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5);
builder.Services.AddMvc();

builder.Services.AddSingleton<ApiDataService>();
builder.Services.AddScoped<BufferService>();

var app = builder.Build();

//app.MapRazorComponents<App>()
//.AddInteractiveServerRenderMode();
//.AddInteractiveWebAssemblyRenderMode();

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

