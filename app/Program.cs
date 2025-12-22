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

// Resolve API base URL against the host base address
var apiBaseUri = Uri.IsWellFormedUriString(apiBase, UriKind.Absolute)
	? new Uri(apiBase)
	: new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBase);

builder.Services.AddScoped<ApiClient>(_ => new ApiClient(new HttpClient
{
	BaseAddress = apiBaseUri
}));
builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddScoped<ChatGptService>();

await builder.Build().RunAsync();
