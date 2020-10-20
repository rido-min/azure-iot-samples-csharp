using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rido;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace pnp_dnd_device
{
    class DndPnPDevice
    {
        IConfiguration config;
        ILogger logger;
        DeviceClient deviceClient;
        int telemetryInterval = 5; //by default
        DiagnosticsComponent diag;

        public DndPnPDevice(IConfiguration c, ILogger log)
        {
            config = c;
            logger = log;
        }

        public async Task Run(CancellationToken token)
        {
            deviceClient = await DeviceClientFactory.CreateDeviceClientAsync(config.GetValue<string>("DeviceConnectionString"), logger, "");
            diag = new DiagnosticsComponent(this);

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback, null);
            await deviceClient.SetMethodHandlerAsync(diag.RebootName, diag.Reboot, diag.Name, token);

            var reported = new TwinCollection();
            reported["serialNumber"] = "S/N-123";
            await deviceClient.UpdateReportedPropertiesAsync(reported);
            await deviceClient.UpdateReportedPropertiesAsync(PnpHelpers.PnpConvention.CreateWritablePropertyResponse("telemetryInterval", telemetryInterval, 201, 0, "Using default value"));

            while (true)
            {
                var temp = new Random().Next(100);
                await deviceClient.SendEventAsync(PnpHelpers.PnpConvention.CreateMessage("temperature",  temp));
                await deviceClient.SendEventAsync(diag.GetWorkingSet());
                Console.Write($"\r [{DateTime.Now.ToLongTimeString()}] \t Sending temperature '{temp}' and workingSet {Environment.WorkingSet} with {telemetryInterval} interval ");
                await Task.Delay(telemetryInterval * 1000);
            }

        }

        public async Task OnReboot(rebootResponse resp)
        {
            Console.WriteLine("REBOOT" + new string('*', 30));
            telemetryInterval = 5;
            TwinCollection reported = new TwinCollection();
            reported[diag.Name] = new
            {
                __t = "c",
                lastReboot = DateTime.Now
            };
            await deviceClient.UpdateReportedPropertiesAsync(reported);
        }

        private async Task DesiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            this.logger.LogWarning($"Received desired updates [{desiredProperties.ToJson()}]");
            var desiredPropertyValue = desiredProperties["telemetryInterval"];
            if (desiredPropertyValue > 0)
            {
                telemetryInterval = desiredPropertyValue;

                await deviceClient.UpdateReportedPropertiesAsync(
                    PnpHelpers.PnpConvention.CreateWritablePropertyResponse("telemetryInterval", telemetryInterval, 200, desiredProperties.Version, "Property synced"));
            }
            else
            {
                await deviceClient.UpdateReportedPropertiesAsync(
                    PnpHelpers.PnpConvention.CreateWritablePropertyResponse("telemetryInterval", telemetryInterval, 500, desiredProperties.Version, "Err. Negative values not supported."));
            }
        }
    }
}
