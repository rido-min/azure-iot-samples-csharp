using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rido;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace pnp_dnd_device
{
    class Thermostat
    {
        IConfiguration config;
        ILogger logger;
        DeviceClient deviceClient;
        int telemetryInterval = 5; //by default

        public Thermostat(IConfiguration c, ILogger log)
        {
            config = c;
            logger = log;
        }

        public async Task Run(CancellationToken token)
        {
            deviceClient = await DeviceClientFactory.CreateDeviceClientAsync(config.GetValue<string>("DeviceConnectionString"), logger, "");
            var diag = new DiagnosticsComponent();


            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback, null);
            await deviceClient.SetMethodHandlerAsync(diag.RebootName, diag.Reboot, diag.Name, token);

            var reported = new TwinCollection();
            reported["serialNumber"] = "S/N-123";
            await deviceClient.UpdateReportedPropertiesAsync(reported);
            await deviceClient.UpdateReportedPropertiesAsync(PnPConvention.CreateAck("telemetryInterval", telemetryInterval, 201, 0, "Using Default Value"));

            while (true)
            {
                await deviceClient.SendEventAsync(PnPConvention.CreateMessage(new { temperature = new Random().Next(100) }));
                await deviceClient.SendEventAsync(diag.GetWorkingSet());
                Console.WriteLine($"Waiting {telemetryInterval} seconds.");
                await Task.Delay(telemetryInterval * 1000);
            }                
                
        }

        public async Task DiagnosticsComponent_OnRebootCommandAsync(object sender, RebootCommandEventArgs e)
        {
            telemetryInterval = 5;
            await deviceClient.UpdateReportedPropertiesAsync(PnPConvention.CreateAck("telemetryInterval", telemetryInterval, 201, 0, "Using Default Value"));
        }

        private async Task DesiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            this.logger.LogWarning($"Received desired updates [{desiredProperties.ToJson()}]");
            var desiredPropertyValue = desiredProperties["telemetryInterval"];
            if (desiredPropertyValue > 0)
            {
                telemetryInterval = desiredPropertyValue;

                await deviceClient.UpdateReportedPropertiesAsync(
                    PnPConvention.CreateAck("telemetryInterval", telemetryInterval, 200, desiredProperties.Version, "Property synced"));
            }
            else
            {
                await deviceClient.UpdateReportedPropertiesAsync(
                    PnPConvention.CreateAck("telemetryInterval", telemetryInterval, 500, desiredProperties.Version, "Err. Negative values not supported."));
                
            }
        }

       

    }
}
