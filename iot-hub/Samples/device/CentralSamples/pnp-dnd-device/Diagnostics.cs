using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace pnp_dnd_device
{
    class rebootRequest
    {
        public int delay = 1;
        public string requestedBy = string.Empty;
        public string requestReason = string.Empty;
        public DateTime requestDate =DateTime.MinValue;
    }

    public class rebootResponse
    {
        public bool rebootAccepted;
        public DateTime rebootRequestReceived;
        public DateTime rebootScheduled;
    }

    public class RebootCommandEventArgs : EventArgs
    {
        public rebootResponse rebootResponse;
        public RebootCommandEventArgs(rebootResponse response)
        {
            rebootResponse = response;
        }
    }


    class DiagnosticsComponent
    {
        public readonly string Name = "diag";
        public readonly string RebootName = "diag*reboot";

        

        public Message GetWorkingSet()
        {
            return PnPConvention.CreateMessage("GetWorkingSet", Environment.WorkingSet, Name);
        }

        public async Task<MethodResponse> Reboot(MethodRequest request, object userContext)
        {
            Console.WriteLine($"Command Running: {request.Name} {request.DataAsJson}");
            var req = JsonConvert.DeserializeObject<rebootRequest>(request.DataAsJson);
            Console.WriteLine(req.requestedBy);

            GC.Collect(2, GCCollectionMode.Forced);

            await Task.Delay(req.delay);
            var resp = new rebootResponse() 
            {
                rebootAccepted = true,
                rebootRequestReceived = DateTime.Now,
                rebootScheduled = DateTime.Now.AddSeconds(req.delay)
            };

            byte[] responsePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp));
            
            return await Task.FromResult(new MethodResponse(responsePayload, (int)PnPConvention.StatusCode.Completed));
        }

     
    }
}

