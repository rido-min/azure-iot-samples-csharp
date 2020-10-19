using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rido;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
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

        public async Task Run()
        {
            deviceClient = await DeviceClientFactory.CreateDeviceClientAsync(config.GetValue<string>("DeviceConnectionString"), logger, "");

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback, null);

            var reported = new TwinCollection();
            reported["serialNumber"] = "S/N-123";
            await deviceClient.UpdateReportedPropertiesAsync(reported);
            await deviceClient.UpdateReportedPropertiesAsync(CreateAck("telemetryInterval", telemetryInterval, 201, 0, "Using Default Value"));

            while (true)
            {
                var telemetryPayload = JsonConvert.SerializeObject(new { temperature = new Random().Next(100) });
                using var message = new Message(Encoding.UTF8.GetBytes(telemetryPayload))
                {
                    ContentEncoding = "utf-8",
                    ContentType = "application/json",
                };
                await deviceClient.SendEventAsync(message);

                Console.WriteLine($"Waiting {telemetryInterval} seconds.");
                await Task.Delay(telemetryInterval * 1000);
            }                
                
        }

        private async Task DesiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            this.logger.LogWarning($"Received desired updates [{desiredProperties.ToJson()}]");
            var desiredPropertyValue = desiredProperties["telemetryInterval"];
            if (desiredPropertyValue > 0)
            {
                telemetryInterval = desiredPropertyValue;
                await AckDesiredPropertyReadAsync("telemetryInterval", desiredPropertyValue, 200, "property synced", desiredProperties.Version);
            }
            else
            {
                await AckDesiredPropertyReadAsync("telemetryInterval", desiredPropertyValue, 500, "Err. Negative values not supported.", desiredProperties.Version);
            }
        }

        public async Task AckDesiredPropertyReadAsync(string propertyName, object payload, int statuscode, string description, long version)
        {
            var ack = CreateAck(propertyName, payload, statuscode, version, description);
            await deviceClient.UpdateReportedPropertiesAsync(ack);
        }

        private TwinCollection CreateAck(string propertyName, object value, int statusCode, long statusVersion, string statusDescription = "")
        {
            TwinCollection ackProp = new TwinCollection();
            ackProp[propertyName] = new
            {
                value = value,
                ac = statusCode,
                av = statusVersion,
                ad = statusDescription
            };
            return ackProp;
        }

    }
}
