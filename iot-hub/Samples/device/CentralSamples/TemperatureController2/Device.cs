using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rido;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using pnp = PnpHelpers.PnpConvention;

namespace TemperatureController2
{
    class Device
    {
        IConfiguration _config;
        ILogger _logger;
        DeviceClient _deviceClient;
        int telemetryInterval = 5; //by default
                                   //DiagnosticsComponent diag;

        private readonly Dictionary<string, Dictionary<DateTimeOffset, double>> _temperatureReadingsDateTimeOffset = new Dictionary<string, Dictionary<DateTimeOffset, double>>();

        private readonly IDictionary<string, DesiredPropertyUpdateCallback> _desiredPropertyUpdateCallbacks = new Dictionary<string, DesiredPropertyUpdateCallback>();

        private readonly Dictionary<string, double> _temperature = new Dictionary<string, double>();

        private readonly Dictionary<string, double> _maxTemp = new Dictionary<string, double>();

        public Device(IConfiguration c, ILogger log)
        {
            _config = c;
            _logger = log;
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            _deviceClient = await DeviceClientFactory.CreateDeviceClientAsync(_config.GetValue<string>("DeviceConnectionString"), _logger, "");

            await _deviceClient.SetMethodHandlerAsync("reboot", HandleRebootCommandAsync, _deviceClient, cancellationToken);
            await _deviceClient.SetMethodHandlerAsync("thermostat1*getMaxMinReport", HandleMaxMinReportCommand, "thermostat1", cancellationToken);
            await _deviceClient.SetMethodHandlerAsync("thermostat2*getMaxMinReport", HandleMaxMinReportCommand, "thermostat2", cancellationToken);


            await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(SetDesiredPropertyUpdateCallback, null, cancellationToken);
            _desiredPropertyUpdateCallbacks.Add("thermostat1", TargetTemperatureUpdateCallbackAsync);
            _desiredPropertyUpdateCallbacks.Add("thermostat2", TargetTemperatureUpdateCallbackAsync);
    
            Console.WriteLine("Device Ready");
    
            await UpdateDeviceInformationAsync(cancellationToken);
            await SendDeviceSerialNumberAsync(cancellationToken);

            bool temperatureReset = true;
            _maxTemp["thermostat1"] = 0d;
            _maxTemp["thermostat2"] = 0d;


            await _deviceClient.UpdateReportedPropertiesAsync(pnp.CreatePropertyPatch("serialNumber", "S/N-123"));
            await _deviceClient.UpdateReportedPropertiesAsync(pnp.CreateWritablePropertyResponse("telemetryInterval", telemetryInterval, 201, 0, "Using default value"));
            int times = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (temperatureReset)
                {
                    // Generate a random value between 5.0°C and 45.0°C for the current temperature reading for each "Thermostat" component.
                    _temperature["thermostat1"] = Math.Round(new Random().NextDouble() * 40.0 + 5.0, 1);
                    _temperature["thermostat2"] = Math.Round(new Random().NextDouble() * 40.0 + 5.0, 1);
                }

                await SendTemperatureAsync("thermostat1", cancellationToken);
                await SendTemperatureAsync("thermostat2", cancellationToken);
                await SendDeviceMemoryAsync(cancellationToken);

                temperatureReset = _temperature["thermostat1"] == 0 && _temperature["thermostat2"] == 0;
                Console.Write("\rSending Telemetry message " + times++);
                await Task.Delay(5 * 1000);
            }
        }

        private async Task<MethodResponse> HandleRebootCommandAsync(MethodRequest request, object userContext)
        {
            try
            {
                int delay = JsonConvert.DeserializeObject<int>(request.DataAsJson);

                _logger.LogDebug($"Command: Received - Rebooting thermostat (resetting temperature reading to 0°C after {delay} seconds).");
                await Task.Delay(delay * 1000);

                _temperature["thermostat1"] = _maxTemp["thermostat1"] = 0;
                _temperature["thermostat2"] = _maxTemp["thermostat2"] = 0;

                _temperatureReadingsDateTimeOffset.Clear();
                Console.WriteLine("Command Reboot");
            }
            catch (JsonReaderException ex)
            {
                _logger.LogDebug($"Command input is invalid: {ex.Message}.");
                return new MethodResponse(500);
            }

            return new MethodResponse(200);
        }

        private Task<MethodResponse> HandleMaxMinReportCommand(MethodRequest request, object userContext)
        {
            try
            {
                string componentName = (string)userContext;
                DateTime sinceInUtc = JsonConvert.DeserializeObject<DateTime>(request.DataAsJson);
                var sinceInDateTimeOffset = new DateTimeOffset(sinceInUtc);

                if (_temperatureReadingsDateTimeOffset.ContainsKey(componentName))
                {
                    _logger.LogDebug($"Command: Received - component=\"{componentName}\", generating max, min and avg temperature " +
                        $"report since {sinceInDateTimeOffset.LocalDateTime}.");

                    Dictionary<DateTimeOffset, double> allReadings = _temperatureReadingsDateTimeOffset[componentName];
                    Dictionary<DateTimeOffset, double> filteredReadings = allReadings.Where(i => i.Key > sinceInDateTimeOffset)
                        .ToDictionary(i => i.Key, i => i.Value);

                    if (filteredReadings != null && filteredReadings.Any())
                    {
                        var report = new
                        {
                            maxTemp = filteredReadings.Values.Max<double>(),
                            minTemp = filteredReadings.Values.Min<double>(),
                            avgTemp = filteredReadings.Values.Average(),
                            startTime = filteredReadings.Keys.Min(),
                            endTime = filteredReadings.Keys.Max(),
                        };

                        _logger.LogDebug($"Command: component=\"{componentName}\", MaxMinReport since {sinceInDateTimeOffset.LocalDateTime}:" +
                            $" maxTemp={report.maxTemp}, minTemp={report.minTemp}, avgTemp={report.avgTemp}, startTime={report.startTime.LocalDateTime}, " +
                            $"endTime={report.endTime.LocalDateTime}");
                        Console.WriteLine("Max Min Report");
                        byte[] responsePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(report));
                        return Task.FromResult(new MethodResponse(responsePayload, 200));
                    }

                    _logger.LogDebug($"Command: component=\"{componentName}\", no relevant readings found since {sinceInDateTimeOffset.LocalDateTime}, " +
                        $"cannot generate any report.");
                    return Task.FromResult(new MethodResponse(404));
                }

                _logger.LogDebug($"Command: component=\"{componentName}\", no temperature readings sent yet, cannot generate any report.");
                return Task.FromResult(new MethodResponse(404));
            }
            catch (JsonReaderException ex)
            {
                _logger.LogDebug($"Command input is invalid: {ex.Message}.");
                return Task.FromResult(new MethodResponse(500));
            }
        }

        private Task SetDesiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            bool callbackNotInvoked = true;

            foreach (KeyValuePair<string, object> propertyUpdate in desiredProperties)
            {
                string componentName = propertyUpdate.Key;
                if (_desiredPropertyUpdateCallbacks.ContainsKey(componentName))
                {
                    _desiredPropertyUpdateCallbacks[componentName]?.Invoke(desiredProperties, componentName);
                    callbackNotInvoked = false;
                }
            }

            if (callbackNotInvoked)
            {
                _logger.LogDebug($"Property: Received a property update that is not implemented by any associated component.");
            }
            Console.WriteLine("DesiredProperties received");
            return Task.CompletedTask;
        }

        private async Task TargetTemperatureUpdateCallbackAsync(TwinCollection desiredProperties, object userContext)
        {
            const string propertyName = "targetTemperature";
            string componentName = (string)userContext;

            bool targetTempUpdateReceived = pnp.TryGetPropertyFromTwin(
                desiredProperties,
                propertyName,
                out double targetTemperature,
                componentName);
            if (!targetTempUpdateReceived)
            {
                _logger.LogDebug($"Property: Update - component=\"{componentName}\", received an update which is not associated with a valid property.\n{desiredProperties.ToJson()}");
                return;
            }

            _logger.LogDebug($"Property: Received - component=\"{componentName}\", {{ \"{propertyName}\": {targetTemperature}°C }}.");

            TwinCollection pendingReportedProperty = pnp.CreateComponentWritablePropertyResponse(
                componentName,
                propertyName,
                targetTemperature,
                201,
                desiredProperties.Version);

            await _deviceClient.UpdateReportedPropertiesAsync(pendingReportedProperty);
            _logger.LogDebug($"Property: Update - component=\"{componentName}\", {{\"{propertyName}\": {targetTemperature} }} in °C is InProgress.");

            // Update Temperature in 2 steps
            double step = (targetTemperature - _temperature[componentName]) / 2d;
            for (int i = 1; i <= 2; i++)
            {
                _temperature[componentName] = Math.Round(_temperature[componentName] + step, 1);
                await Task.Delay(6 * 1000);
            }

            TwinCollection completedReportedProperty = pnp.CreateComponentWritablePropertyResponse(
                componentName,
                propertyName,
                _temperature[componentName],
                200,
                desiredProperties.Version,
                "Successfully updated target temperature");

            await _deviceClient.UpdateReportedPropertiesAsync(completedReportedProperty);
            _logger.LogDebug($"Property: Update - component=\"{componentName}\", {{\"{propertyName}\": {_temperature[componentName]} }} in °C is Completed");
        }

        private async Task UpdateDeviceInformationAsync(CancellationToken cancellationToken)
        {
            const string componentName = "deviceInformation";

            TwinCollection deviceInfoTc = pnp.CreateComponentPropertyPatch(
                componentName,
                new Dictionary<string, object>
                {
                    { "manufacturer", "element15" },
                    { "model", "ModelIDxcdvmk" },
                    { "swVersion", "1.0.0" },
                    { "osName", myOperatingSystem.isWindows() ? "Windows" : "Linux" },
                    { "processorArchitecture", "64-bit" },
                    { "processorManufacturer", "Intel" },
                    { "totalStorage", 256 },
                    { "totalMemory", 1024 },
                });
            Console.WriteLine("Sent DeviceInfo");
            await _deviceClient.UpdateReportedPropertiesAsync(deviceInfoTc, cancellationToken);
            _logger.LogDebug($"Property: Update - component = '{componentName}', properties update is complete.");
        }

        private async Task SendDeviceSerialNumberAsync(CancellationToken cancellationToken)
        {
            string SerialNumber = "SR-123456";
            const string propertyName = "serialNumber";
            TwinCollection reportedProperties = pnp.CreatePropertyPatch(propertyName, SerialNumber);

            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken);
            _logger.LogDebug($"Property: Update - {{ \"{propertyName}\": \"{SerialNumber}\" }} is complete.");
        }

        private async Task SendTemperatureAsync(string componentName, CancellationToken cancellationToken)
        {
            await SendTemperatureTelemetryAsync(componentName, cancellationToken);

            double maxTemp = _temperatureReadingsDateTimeOffset[componentName].Values.Max<double>();
            if (maxTemp > _maxTemp[componentName])
            {
                _maxTemp[componentName] = maxTemp;
                await UpdateMaxTemperatureSinceLastRebootAsync(componentName, cancellationToken);
            }
        }

        private async Task SendTemperatureTelemetryAsync(string componentName, CancellationToken cancellationToken)
        {
            const string telemetryName = "temperature";
            double currentTemperature = _temperature[componentName];
            using Message msg = pnp.CreateMessage(telemetryName, currentTemperature, componentName);

            await _deviceClient.SendEventAsync(msg, cancellationToken);
            _logger.LogDebug($"Telemetry: Sent - component=\"{componentName}\", {{ \"{telemetryName}\": {currentTemperature} }} in °C.");

            if (_temperatureReadingsDateTimeOffset.ContainsKey(componentName))
            {
                _temperatureReadingsDateTimeOffset[componentName].TryAdd(DateTimeOffset.UtcNow, currentTemperature);
            }
            else
            {
                _temperatureReadingsDateTimeOffset.TryAdd(
                    componentName,
                    new Dictionary<DateTimeOffset, double>
                    {
                        { DateTimeOffset.UtcNow, currentTemperature },
                    });
            }
        }

        private async Task UpdateMaxTemperatureSinceLastRebootAsync(string componentName, CancellationToken cancellationToken)
        {
            const string propertyName = "maxTempSinceLastReboot";
            double maxTemp = _maxTemp[componentName];
            TwinCollection reportedProperties = pnp.CreateComponentPropertyPatch(componentName, propertyName, maxTemp);

            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken);
            _logger.LogDebug($"Property: Update - component=\"{componentName}\", {{ \"{propertyName}\": {maxTemp} }} in °C is complete.");
        }

        private async Task SendDeviceMemoryAsync(CancellationToken cancellationToken)
        {
            const string workingSetName = "workingSet";

            long workingSet = Process.GetCurrentProcess().PrivateMemorySize64 / 1024;

            var telemetry = new Dictionary<string, object>
            {
                { workingSetName, workingSet },
            };

            using Message msg = pnp.CreateMessage(telemetry);

            await _deviceClient.SendEventAsync(msg, cancellationToken);
            _logger.LogDebug($"Telemetry: Sent - {JsonConvert.SerializeObject(telemetry)} in KB.");
        }
    }

    

    public static class myOperatingSystem
    {
        public static bool isWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool isMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool isLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }
}
