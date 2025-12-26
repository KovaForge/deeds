using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using System.Reflection;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication() // this line satisfies AZFW0014
    .ConfigureServices((context, services) =>
    {
        // bind DB connection string from configuration/environment
        services.AddOptions<DbOptions>()
            .Configure<IConfiguration>((opts, cfg) =>
            {
                var raw = cfg["DB"] ?? string.Empty;
                opts.ConnectionString = ConnectionStringHelper.Normalize(raw);
            });

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DbOptions>>().Value);
    })
    .Build();

var dbOptions = host.Services.GetRequiredService<DbOptions>();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
{
    logger.LogWarning("DB connection string not provided; database-dependent endpoints will fail until 'DB' is set.");
}
else
{
    try
    {
        var sqlFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "sql");
        await Migrations.ApplyAsync(dbOptions.ConnectionString, sqlFolder, logger);
        await Data.EnsureSchema(dbOptions.ConnectionString);
        logger.LogInformation("Database initialized successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply migrations: {Message}. Stack: {StackTrace}", ex.Message, ex.StackTrace);
        throw;
    }
}

host.Run();
