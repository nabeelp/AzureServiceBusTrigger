using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureServiceBusTrigger.Models
{
    public class TriggerRecord : TableEntity
    {
        public TriggerRecord(string workflowId)
        {
            this.PartitionKey = workflowId;
            this.RowKey = "";
        }

        public TriggerRecord() { }

        public string subscriptionInfo { get; set; }
    }
}