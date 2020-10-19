using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace pnp_dnd_device
{
    class PnPConvention
    {
        internal enum StatusCode
        {
            Completed = 200,
            InProgress = 202,
            NotFound = 404,
            BadRequest = 400
        }

        public static Message CreateMessage(object objectToSerialize)
        {
            var telemetryPayload = JsonConvert.SerializeObject(objectToSerialize);
            var message = new Message(Encoding.UTF8.GetBytes(telemetryPayload))
            {
                ContentEncoding = "utf-8",
                ContentType = "application/json",
            };
            return message;
        }

        public static Message CreateMessage(string telemetryName, object telemetryValue, string componentName = default, Encoding encoding = default)
        {
            if (string.IsNullOrWhiteSpace(telemetryName))
            {
                throw new ArgumentNullException(nameof(telemetryName));
            }
            if (telemetryValue == null)
            {
                throw new ArgumentNullException(nameof(telemetryValue));
            }

            return CreateMessage(new Dictionary<string, object> { { telemetryName, telemetryValue } }, componentName, encoding);
        }

        /// <summary>
        /// Create a plug and play compatible telemetry message.
        /// </summary>
        /// <param name="componentName">The name of the component in which the telemetry is defined. Can be null for telemetry defined under the root interface.</param>
        /// <param name="telemetryPairs">The unserialized name and value telemetry pairs, as defined in the DTDL interface. Names must be 64 characters or less. For more details see
        /// <see href="https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md#telemetry"/>.</param>
        /// <param name="encoding">The character encoding to be used when encoding the message body to bytes. This defaults to utf-8.</param>
        /// <returns>A plug and play compatible telemetry message, which can be sent to IoT Hub. The caller must dispose this object when finished.</returns>
        public static Message CreateMessage(IDictionary<string, object> telemetryPairs, string componentName = default, Encoding encoding = default)
        {
            if (telemetryPairs == null)
            {
                throw new ArgumentNullException(nameof(telemetryPairs));
            }

            Encoding messageEncoding = encoding ?? Encoding.UTF8;
            string payload = JsonConvert.SerializeObject(telemetryPairs);
            var message = new Message(messageEncoding.GetBytes(payload))
            {
                ContentEncoding = messageEncoding.WebName,
                ContentType = "application/json",
            };

            if (!string.IsNullOrWhiteSpace(componentName))
            {
                message.ComponentName = componentName;
            }

            return message;
        }

        public static TwinCollection CreateAck(string componentName, string propertyName, object value, int statusCode, long statusVersion, string statusDescription = "")
        {
            TwinCollection ack = new TwinCollection();
            var ackProps = new TwinCollection();
            ackProps["value"] = value;
            ackProps["ac"] = statusCode;
            ackProps["av"] = statusVersion;
            if (!string.IsNullOrEmpty(statusDescription)) ackProps["ad"] = statusDescription;
            TwinCollection ackChildren = new TwinCollection();
            ackChildren["__t"] = "c"; // TODO: Review, should the ACK require the flag
            ackChildren[propertyName] = ackProps;
            ack[componentName] = ackChildren;
            return ack;
        }
    

        public static TwinCollection CreateAck(string propertyName, object value, int statusCode, long statusVersion, string statusDescription = "")
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
