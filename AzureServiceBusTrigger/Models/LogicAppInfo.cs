using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace AzureServiceBusTrigger.Models
{
    [DataContract]
    public class LogicAppInfo
    {
        [DataMember]
        [Required]
        public string WorkflowId { get; set; }
        [DataMember]
        [Required]
        public string Name { get; set; }
        [DataMember]
        [Required]
        public string CallbackUrl { get; set; }
    }
}