using AzureServiceBusTrigger.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace AzureServiceBusTrigger.Models
{
    [DataContract]
    public class SubscriptionInfo
    {
        [DataMember]
        [Required]
        public LogicAppInfo logicAppInfo { get; set; }
        [DataMember]
        [Required]
        public TriggerConfig triggerConfig { get; set; }
        [IgnoreDataMember]
        public ServiceBusListener listener { get; set; }
    }
}