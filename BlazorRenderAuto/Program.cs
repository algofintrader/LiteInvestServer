using BlazorRenderAuto.Client.Pages;
using BlazorRenderAuto.Client.Services;
using BlazorRenderAuto.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddCors(options =>
{
	options.AddPolicy("aaa",
		builder =>
		{
			builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
				.SetIsOriginAllowedToAllowWildcardSubdomains();
		});
});



//builder.Services.AddScoped<ApiDataServiceTest>();
builder.Services.AddScoped<JsInteropService>();

builder.Services.AddSingleton<ApiDataService>();
//builder.Services.AddTelerikBlazor();



builder.Services.AddMvc();

var app = builder.Build();

app.UseCors("aaa");


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Home).Assembly);
    //.AddAdditionalAssemblies(typeof(BlazorRenderAuto.Client._Imports).Assembly);

app.Run();
