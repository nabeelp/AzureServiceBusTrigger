using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace AzureServiceBusTrigger.Models
{
    public class ServiceBusMessage
    {
        public JToken body;
        public ServiceBusMessage(BrokeredMessage message)
        {
            body = JToken.FromObject(message);
            string messageBody = new StreamReader(message.GetBody<Stream>(), Encoding.UTF8).ReadToEnd();
            body["ContentData"] = messageBody;
        }
    }
}