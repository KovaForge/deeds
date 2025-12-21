using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication() // this line satisfies AZFW0014
    .ConfigureServices((context, services) =>
    {
        // bind DB connection string from configuration/environment
        services.AddOptions<DbOptions>()
            .Configure<IConfiguration>((opts, cfg) =>
            {
                opts.ConnectionString = cfg["DB"];
            });

        services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<DbOptions>>().Value);
    })
    .Build();

var dbOptions = host.Services.GetRequiredService<DbOptions>();
if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
{
    host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup")
        .LogWarning("DB connection string not provided; database-dependent endpoints will fail until 'DB' is set.");
}
else
{
    await Data.EnsureSchema(dbOptions.ConnectionString);
}

host.Run();
