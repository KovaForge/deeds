using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GoodDeeds.Client;
using GoodDeeds.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Load API base URL from config; fallback to /api/ if not specified.
var apiBase = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBase))
{
	apiBase = "/api/";
}

if (!apiBase.EndsWith('/'))
{
	apiBase += "/";
}

builder.Services.AddScoped<ApiClient>(_ => new ApiClient(new HttpClient
{
	BaseAddress = new Uri(apiBase, UriKind.RelativeOrAbsolute)
}));
builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddScoped<ChatGptService>();

await builder.Build().RunAsync();
