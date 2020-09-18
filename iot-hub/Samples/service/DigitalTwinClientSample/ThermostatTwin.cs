// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Devices.Serialization;
using Newtonsoft.Json;
using System;

namespace DigitalTwinClientSample
{
    public class ThermostatTwin : BasicDigitalTwin
    {
        [JsonProperty("$metadata")]
        public new ThermostatMetadata Metadata { get; set; }

        [JsonProperty("maxTempSinceLastReboot")]
        public int MaxTempSinceLastReboot { get; set; }

        [JsonProperty("targetTemperature")]
        public int TargetTemperature { get; set; }
    }

    public class ThermostatMetadata : DigitalTwinMetadata
    {
        [JsonProperty("maxTempSinceLastReboot")]
        public ReportedPropertyMetadata MaxTempSinceLastReboot { get; set; }

        [JsonProperty("targetTemperature")]
        public WritableProperty TargetTemperature { get; set; }
    }

    public class ReportedPropertyMetadata
    {
        [JsonProperty("lastUpdateTime")]
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
