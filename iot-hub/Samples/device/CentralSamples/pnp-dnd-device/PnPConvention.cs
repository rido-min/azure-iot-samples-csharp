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
