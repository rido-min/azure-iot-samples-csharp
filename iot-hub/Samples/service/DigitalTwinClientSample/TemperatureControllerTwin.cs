// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Devices.Serialization;
using Newtonsoft.Json;

namespace DigitalTwinClientSample
{
    public class TemperatureControllerTwin : BasicDigitalTwin
    {
        [JsonProperty("$metadata")]
        public new TemperatureControllerMetadata Metadata { get; set; }

        [JsonProperty("serialNumber")]
        public string SerialNumber { get; set; }
    }

    public class TemperatureControllerMetadata : DigitalTwinMetadata
    {
        [JsonProperty("serialNumber")]
        public ReportedPropertyMetadata SerialNumber { get; set; }
    }
}
