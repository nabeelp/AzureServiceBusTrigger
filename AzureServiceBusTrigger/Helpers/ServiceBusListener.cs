using Microsoft.ServiceBus.Messaging;
using AzureServiceBusTrigger.Models;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AzureServiceBusTrigger.Helpers
{
    public class ServiceBusListener
    {
        MessagingFactory messagingFactory;
        private QueueClient queueReceiver;
        private SubscriptionClient subscriptionReceiver;
        private string callbackUrl;
        private string workflowId;
        private string logMessageStub;
        public bool isListening = false;
        
        public ServiceBusListener(SubscriptionInfo subscriptionInfo)
        {
            // keep a record of the callback URI and workflowId
            callbackUrl = subscriptionInfo.logicAppInfo.CallbackUrl;
            workflowId = subscriptionInfo.logicAppInfo.WorkflowId;

            // initialise logMessageStub to standardise the tracing output
            logMessageStub = String.Format("{0} ({1}), ", subscriptionInfo.logicAppInfo.Name, Guid.NewGuid().ToString());
            if (subscriptionInfo.triggerConfig.QueueName != null && subscriptionInfo.triggerConfig.QueueName.Length > 1)
            {
                logMessageStub += String.Format("queue={0}", subscriptionInfo.triggerConfig.QueueName);
            }
            else
            {
                logMessageStub += String.Format("topic={0}, subscription={1}", subscriptionInfo.triggerConfig.TopicName, subscriptionInfo.triggerConfig.SubscriptionName);
            }

            // initialise the messaging factory, using either the provided connection string, or the one in config
            string connectionString = String.Empty;
            if (String.IsNullOrEmpty(subscriptionInfo.triggerConfig.ServiceBusConnectionString))
            {
                connectionString = ConfigurationManager.AppSettings["MyServiceBusConnectionString"];
                Trace.WriteLine(String.Format("{0}: {1} Service Bus connection string retrieved via configuration manager, workflow id: {2}",
                    logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), workflowId));
            }
            else
            {
                connectionString = subscriptionInfo.triggerConfig.ServiceBusConnectionString;
                Trace.WriteLine(String.Format("{0}: {1} Service Bus connection string supplied via subscription request, workflow id: {2}",
                    logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), workflowId));
            }

            if (String.IsNullOrEmpty(connectionString))
            {
                throw new Exception("No service bus connection string supplied or available in configuration");
            }
            else
            {
                try
                {
                    // add the OperationTimeout attribute if not already part of the connection string
                    if (!connectionString.Contains("OperationTimeout="))
                    {
                        int operationTimeoutMinutes = 60;
                        int.TryParse(ConfigurationManager.AppSettings["ServiceBusOperationTimeoutMinutes"], out operationTimeoutMinutes);
                        TimeSpan operationTimeout = new TimeSpan(0, operationTimeoutMinutes, 0);
                        connectionString += ";OperationTimeout=" + operationTimeout.ToString();
                    }

                    // create the messaging factory
                    messagingFactory = MessagingFactory.CreateFromConnectionString(connectionString);
                    Trace.WriteLine(String.Format("{0}: {1} Messaging factory created with timeout of {2} minutes, workflow id: {3}",
                        logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), messagingFactory.GetSettings().OperationTimeout.TotalMinutes, workflowId));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(String.Format("{0}: {1} Error encountered connecting to Azure Service Bus. Connection string: {2}. Error details: {3}",
                        logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), connectionString, ex.ToString()));
                    throw ex;
                }
            }
        }

        public async Task StartListening(TriggerConfig triggerInput)
        {
            // Configure the callback options
            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = false;
            options.MaxConcurrentCalls = 10;

            // create the relevant client and the onMessage listener
            if (triggerInput.QueueName != null && triggerInput.QueueName.Length > 1)
            {
                queueReceiver = messagingFactory.CreateQueueClient(triggerInput.QueueName, ReceiveMode.PeekLock);
                queueReceiver.OnMessageAsync(async message =>
                {
                    await processMessage(message, logMessageStub);
                }, options);
                isListening = true;
            }
            else
            {
                subscriptionReceiver = messagingFactory.CreateSubscriptionClient(triggerInput.TopicName, triggerInput.SubscriptionName, ReceiveMode.PeekLock);
                subscriptionReceiver.OnMessageAsync(async message =>
                {
                    await processMessage(message, logMessageStub);
                }, options);
                isListening = true;
            }
        }

        private async Task processMessage(BrokeredMessage message, string logMessageStub)
        {
            try
            {
                // prepare to make callback request
                HttpWebRequest newRequest = WebRequest.CreateHttp(callbackUrl);
                newRequest.Method = "POST";
                newRequest.ContentType = "application/json";
                string postData = new ServiceBusMessage(message).body.ToString();
                byte[] byteArray = Encoding.UTF8.GetBytes(postData);

                // send to the callback Uri
                Trace.WriteLine(String.Format("{0}: {1} Posting to callback: {2}",
                    logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), callbackUrl));
                using (Stream requestBody = newRequest.GetRequestStream())
                {
                    requestBody.Write(byteArray, 0, byteArray.Length);
                    newRequest.GetResponse();
                }
                Trace.TraceInformation(String.Format("{0}: {1} Callback submitted successfully, workflow id: {2}",
                    logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), workflowId));
                await message.CompleteAsync();
            }
            catch (Exception ex)
            {
                Trace.TraceError(String.Format("{0}: {1} Callback failed, workflow id: {2}, {3}{4}",
                    logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), workflowId, Environment.NewLine, ex.ToString()));
                await message.AbandonAsync();
            }

        }

        public async Task StopListening()
        {
            try
            {
                // close any necesary receivers and the factory
                if (queueReceiver != null && queueReceiver.IsClosed == false)
                {
                    await queueReceiver.CloseAsync();
                }
                if (subscriptionReceiver != null && subscriptionReceiver.IsClosed == false)
                {
                    await subscriptionReceiver.CloseAsync();
                }
                await messagingFactory.CloseAsync();
                isListening = false;

                // remove the register from the store
                InMemoryTriggerStore.Instance.GetStore().Remove(workflowId);
                Trace.TraceInformation(String.Format("{0}: {1} Stopped listening, workflow id: {2}",
                    logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), workflowId));
            }
            catch (Exception ex)
            {
                Trace.TraceError(String.Format("{0}: {1} Attempt to stop listening failed, workflow id: {2}, {3}{4}",
                    logMessageStub, DateTime.Now.ToString("yyyyMMdd HHmmss"), workflowId, Environment.NewLine, ex.ToString()));
            }
        }
    }
}