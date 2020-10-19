using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace pnp_dnd_device
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource(360000);
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            ILogger logger = LoggerFactory.Create(builder =>
                builder
                .AddConfiguration(config.GetSection("Logging"))
                .AddDebug()
                .AddConsole()
            ).CreateLogger<Program>();

            logger.LogInformation("Starting .... ");
            await new Thermostat(config, logger).Run(cancellationTokenSource.Token);
        }
    }
}
