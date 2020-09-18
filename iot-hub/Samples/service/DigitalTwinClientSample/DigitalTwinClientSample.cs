// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DigitalTwinClientSample;
using Microsoft.Azure.Devices.Serialization;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Samples
{
    public class DigitalTwinClientSample
    {
        private static readonly Random Random = new Random();
        private readonly DigitalTwinClient _digitalTwinClient;
        private readonly string _digitalTwinId;

        public DigitalTwinClientSample(DigitalTwinClient client, string digitalTwinId)
        {
            _digitalTwinClient = client ?? throw new ArgumentNullException(nameof(DigitalTwinClient));
            _digitalTwinId = digitalTwinId ?? throw new ArgumentNullException(nameof(digitalTwinId));
        }

        public async Task RunSampleAsync()
        {
            Console.WriteLine("\n\n========== Operations with \"thermostat\" - root component only ==========");
            var thermostat = "thermostat";
            await GetAndUpdateDigitalTwin(thermostat, true);

            Console.WriteLine("\n\n========== Operations with \"tc\" - with sub-component ==========");
            var temperatureController = "tc";
            await GetAndUpdateDigitalTwin(temperatureController);
            await InvokeCommands(temperatureController);
        }

        private async Task GetAndUpdateDigitalTwin(string digitalTwinId, bool rootComponent = false)
        {
            string propertyName = "targetTemperature";

            if (rootComponent)
            {
                int propertyValue = Random.Next(0, 100);

                var twin = await GetAndPrintDigitalTwin<ThermostatTwin>(digitalTwinId);
                var targetTemperature = twin.TargetTemperature;
                Console.WriteLine($"\nCurrent \"{propertyName}\" under \"root component\": {targetTemperature}");

                var targetTempDesiredValue = twin.Metadata.TargetTemperature.DesiredValue;
                var targetTempDesiredVersion = twin.Metadata.TargetTemperature.DesiredVersion;
                Console.WriteLine($"Current desired value = {targetTempDesiredValue}, version = {targetTempDesiredVersion}");

                // Update the property "targetTemperature" under root component.
                Console.WriteLine($"\nUpdate the property \"{propertyName}\" under \"root component\" to {propertyValue}.");
                var op = new UpdateOperationsUtility();
                op.AppendReplaceOp($"/{propertyName}", propertyValue);
                var updateResponse = await _digitalTwinClient.UpdateAsync(digitalTwinId, op.Serialize());
                Console.WriteLine($"Update operation was: {updateResponse.Response.StatusCode}");

                var twin2 = await GetAndPrintDigitalTwin<ThermostatTwin>(digitalTwinId);
                var targetTempDesiredValue2 = twin2.Metadata.TargetTemperature.DesiredValue;
                var targetTempDesiredVersion2 = twin2.Metadata.TargetTemperature.DesiredVersion;
                Console.WriteLine($"\nCurrent desired value = {targetTempDesiredValue2}, version = {targetTempDesiredVersion2}");
            }
            else
            {
                string componentName = "thermostat1";
                int propertyValue = Random.Next(0, 100);

                var twin = await GetAndPrintDigitalTwin<TemperatureControllerTwin>(digitalTwinId);
                var targetTemperature = twin.Thermostat1.TargetTemperature;
                Console.WriteLine($"\nCurrent \"{propertyName}\" under \"{componentName}\": {targetTemperature}");

                var targetTempDesiredValue = twin.Thermostat1.Metadata.TargetTemperature.DesiredValue;
                var targetTempDesiredVersion = twin.Thermostat1.Metadata.TargetTemperature.DesiredVersion;
                Console.WriteLine($"Current desired value = {targetTempDesiredValue}, version = {targetTempDesiredVersion}");

                // Update the property "targetTemperature" under "thermostat1" component.
                Console.WriteLine($"\nUpdate the property \"{propertyName}\" under \"{componentName}\" to {propertyValue}.");
                var op = new UpdateOperationsUtility();
                op.AppendReplaceOp($"/{componentName}/{propertyName}", propertyValue);
                var updateResponse = await _digitalTwinClient.UpdateAsync(digitalTwinId, op.Serialize());
                Console.WriteLine($"\nUpdate operation was: {updateResponse.Response.StatusCode}");

                var twin2 = await GetAndPrintDigitalTwin<TemperatureControllerTwin>(digitalTwinId);
                var targetTempDesiredValue2 = twin2.Thermostat1.Metadata.TargetTemperature.DesiredValue;
                var targetTempDesiredVersion2 = twin2.Thermostat1.Metadata.TargetTemperature.DesiredVersion;
                Console.WriteLine($"\nCurrent desired value = {targetTempDesiredValue2}, version = {targetTempDesiredVersion2}");
            }
        }

        private async Task<T> GetAndPrintDigitalTwin<T>(string digitalTwinId)
        {
            var getResponse = await _digitalTwinClient.GetAsync<T>(digitalTwinId);
            var twin = getResponse.Body;
            Console.WriteLine($"\nComplete twin: \n{JsonConvert.SerializeObject(twin, Formatting.Indented)}");

            return twin;
        }

        private async Task InvokeCommands(string digitalTwinId)
        {
            // Invoke the command "getMaxMinReport" on component "thermostat1".
            string componentName = "thermostat1";
            string componentCommandName = "getMaxMinReport";
            DateTimeOffset since = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(2));
            var rebootResponse = await _digitalTwinClient.InvokeComponentCommandAsync(digitalTwinId, componentName, componentCommandName, JsonConvert.SerializeObject(since));

            Console.WriteLine($"\nCommand \"{componentCommandName}\" invoked under \"{componentName}\", device response was {rebootResponse.Headers.XMsCommandStatuscode}");
            Console.WriteLine($"\t{rebootResponse.Body}");

            // Invoke the command "reboot" on the root component.
            string rootCommandName = "reboot";
            int delay = 1;
            var reportResponse = await _digitalTwinClient.InvokeCommandAsync(digitalTwinId, rootCommandName, JsonConvert.SerializeObject(delay));

            Console.WriteLine($"\nCommand \"{rootCommandName}\" invoked under \"root component\", device response was {reportResponse.Headers.XMsCommandStatuscode}");
            Console.WriteLine($"\t{reportResponse.Body}");
        }
    }
}
