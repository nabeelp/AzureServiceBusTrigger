using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using AzureServiceBusTrigger.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;

namespace AzureServiceBusTrigger.Helpers
{
    public class InMemoryTriggerStore
    {
        private static InMemoryTriggerStore instance;
        private IDictionary<string, SubscriptionInfo> _store;
        private string azureTableConnectionString;
        private static CloudTable triggerTable;

        private InMemoryTriggerStore()
        {
            // initialise the memory store
            _store = new Dictionary<string, SubscriptionInfo>();

            /// initialise a table store as well for durable storage
            azureTableConnectionString = ConfigurationManager.AppSettings["MyAzureTableConnectionString"];
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(azureTableConnectionString);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            triggerTable = tableClient.GetTableReference("AzureServiceBusTriggerStore");
            triggerTable.CreateIfNotExists();
        }

        public IDictionary<string, SubscriptionInfo> GetStore()
        {
            return _store;
        }

        public static InMemoryTriggerStore Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new InMemoryTriggerStore();

                    // populate the trigger store from the table
                    IEnumerable<TriggerRecord> triggers = triggerTable.ExecuteQuery<TriggerRecord>(new TableQuery<TriggerRecord>());
                    foreach (TriggerRecord trigger in triggers)
                    {
                        // according to https://msdn.microsoft.com/en-us/library/azure/dd179338.aspx, PartitionKeys do not allow the "/" character, so when inserting we replaced with "||", now we have to reverse that
                        trigger.PartitionKey = trigger.PartitionKey.Replace("||", "/");
                        SubscriptionInfo subscriptionInfo = JsonConvert.DeserializeObject<SubscriptionInfo>(trigger.subscriptionInfo);
                        instance.RegisterTrigger(trigger.PartitionKey, subscriptionInfo, false);
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// The method that registers Service Bus listeners and assigns them to a receive event.  When an event from the service bus listener is raised, trigger the callbackURL
        /// </summary>
        /// <param name="logicAppId"></param>
        /// <param name="triggerInput"></param>
        /// <returns></returns>
        public async Task RegisterTrigger(string workflowId, SubscriptionInfo subscriptionInfo, bool addToTable = true)
        {
            // Add to the table store 
            if (addToTable)
            {
                // according to https://msdn.microsoft.com/en-us/library/azure/dd179338.aspx, PartitionKeys do not allow the "/" character, so when inserting we replaced with "||"
                TriggerRecord newTrigger = new TriggerRecord(workflowId.Replace("/", "||"));
                string jsonString = JsonConvert.SerializeObject(subscriptionInfo);
                newTrigger.subscriptionInfo = jsonString;
                TableOperation insertOperation = TableOperation.Insert(newTrigger);
                try
                {
                    triggerTable.Execute(insertOperation);
                }
                catch (StorageException ex)
                {
                    throw new Exception("Error occurred while trying to add trigger to Azure Table. Error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorMessage, ex);
                }
            }

            // Create a new listener for this trigger
            ServiceBusListener listener = new ServiceBusListener(subscriptionInfo);
            HostingEnvironment.QueueBackgroundWorkItem(async ct => await listener.StartListening(subscriptionInfo.triggerConfig));
            subscriptionInfo.listener = listener;

            // Register the logicAppId in the store, so on subsequent checks from the logic app we don't spin up a new set of listeners
            _store[workflowId] = subscriptionInfo;
        }

        public async Task UnregisterTrigger(string workflowId)
        {
            if (_store.ContainsKey(workflowId))
            {
                // Remove from the table 
                // according to https://msdn.microsoft.com/en-us/library/azure/dd179338.aspx, PartitionKeys do not allow the "/" character, so when inserting we replaced with "||"
                TriggerRecord oldTrigger = new TriggerRecord(workflowId.Replace("/", "||"));
                oldTrigger.ETag = "*";
                TableOperation deleteOperation = TableOperation.Delete(oldTrigger);
                try
                { 
                    triggerTable.Execute(deleteOperation);
                }
                catch (StorageException ex)
                {
                    throw new Exception("Error occurred while trying to remove trigger from Azure Table. Error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorMessage, ex);
                }

                // stop listening
                await _store[workflowId].listener.StopListening();
            }
        }
    }

}