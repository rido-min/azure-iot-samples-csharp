// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DigitalTwinClientSample;
using Microsoft.Azure.Devices.ModernDotNet.DigitalTwin.Serialization;
using Microsoft.Azure.Devices.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Samples
{
    public class DigitalTwinClientSample
    {
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
            await GetAndUpdateDigitalTwin("thermostat", true);

            Console.WriteLine("\n\n========== Operations with \"tc\" - with sub-component ==========");
            await GetAndUpdateDigitalTwin("tc");
        }

        private async Task GetAndUpdateDigitalTwin(string digitalTwinId, bool rootComponent = false)
        {
            string propertyName = "targetTemperature";
            int propertyValue = new Random().Next(0, 100);
            if (rootComponent)
            {
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

                var twin = await GetAndPrintDigitalTwin<BasicDigitalTwin>(digitalTwinId);
                JObject thermostat1 = JObject.FromObject(twin.CustomProperties[componentName]);
                var thermostat1TargetTemperature = thermostat1.GetValue(propertyName);
                Console.WriteLine($"\nCurrent \"{propertyName}\" under \"{componentName}\": {thermostat1TargetTemperature}");

                // Update the property "targetTemperature" under "thermostat1" component.
                Console.WriteLine($"\nUpdate the property \"{propertyName}\" under \"{componentName}\" to {propertyValue}.");
                var op = new UpdateOperationsUtility();
                op.AppendReplaceOp($"/{componentName}/{propertyName}", propertyValue);
                var updateResponse = await _digitalTwinClient.UpdateAsync(digitalTwinId, op.Serialize());
                Console.WriteLine($"\nUpdate operation was: {updateResponse.Response.StatusCode}");

                var twin2 = await GetAndPrintDigitalTwin<BasicDigitalTwin>(digitalTwinId);
                JObject updatedThermostat1 = JObject.FromObject(twin2.CustomProperties[componentName]);
                var updatedThermostat1TargetTemperature = updatedThermostat1.GetValue(propertyName);
                Console.WriteLine($"\nCurrent \"{propertyName}\" under \"{componentName}\": {updatedThermostat1TargetTemperature}");
            }
        }

        private async Task<T> GetAndPrintDigitalTwin<T>(string digitalTwinId)
        {
            var getResponse = await _digitalTwinClient.GetAsync<T>(digitalTwinId);
            var twin = getResponse.Body;
            Console.WriteLine($"\nComplete twin: \n{JsonConvert.SerializeObject(twin, Formatting.Indented)}");

            return twin;
        }
    }
}
