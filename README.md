# AzureServiceBusTrigger
A sample HTTP WebHook-based solution, using the long-polling support and the onMessage event within the Azure Service Bus libraries, to create an event-based trigger which will react on new messages being received on a queue or a subscription. Combining this with HTTP WebHook support enables the solution to run as an Azure Web Site, and post received message info to a callback URL, such as the one available via Azure Logic Apps WebHook triggers.

## Usage Guide ##
To use this solution, we first need to get the service deployed and configured:
- Open the solution in Visual Studio
- Configure Application Insights for your instance
- Compile and deploy to a new resource group
- Navigate to the "Application settings" blade in the Azure Portal for the created Web App resource
- Add the following app settings:
  - MyServiceBusConnectionString, set this to the connection string for the Azure Service Bus namespace you want to listen on, in the format Endpoint=sb://[ServiceBusNamespace].servicebus.windows.net/;SharedAccessKeyName=[ServiceBusKeyName];SharedAccessKey=[ServiceBusKey]
  - ServiceBusOperationTimeoutMinutes, set this to the number of minutes you would like to wait for a message, before timing out.  This is used to establish long polling.
  - MyAzureTableConnectionString, set this to the connection string for an Azure Table, where the HTTP WebHook information will be persisted when an HTTP WebHook subscribe request is received. Entries in this table will also be used to initialise the service, should the service be restarted for any reason.
  - LimitToCurrentSubscription, set this to True if you want to limit the use of this service to only those requests that originate from Logic Apps in a specific subscription.  Otherwise, set this to False.
  - CurrentSubscriptionID, if the LimitToCurrentSubscription is set to True the value in this field will be compared with the subscription ID value contained in the Logic App callback URL.
 
To reference the deployed service from a Logic App:
- Create a new logic app
- Add a new HTTP WebHook trigger, and set the properties as follows:
  - Subscribe Method: POST
  - Subscribe URI: http://[yourservice].azurewebsites.net/trigger/push/subscribe
  - Subscribe Body: set to the following JSON, if you are listening to a queue: 
  ```json
  {
  	"logicAppInfo" :
  	{
  		"CallbackUrl" : "@{listCallbackUrl()}",
  		"Name" : "@{workflow().name}",
  		"workflowId" : "@{workflow().id}"
  	},
  	"triggerConfig" :
  	{
  		"QueueName" : "queue1"
  	}
  }
  ```
  *OR*
  Set it to the following if listening on a subscription:
  ```json
  {
  	"logicAppInfo" :
  	{
  		"CallbackUrl" : "@{listCallbackUrl()}",
  		"Name" : "@{workflow().name}",
  		"workflowId" : "@{workflow().id}"
  	},
  	"triggerConfig" :
  	{
  		"TopicName" : "string",
  		"SubscriptionName" : "string"
  	}
  }
  ```
  - Unsubscribe Method: POST
  - Unsubscribe URI: http://[yourservice].azurewebsites.net/trigger/push/unsubscribe
  - Unsubscribe Body: set to the following JSON: 
  ```json
  {
  	"logicAppInfo" :
  	{
  		"CallbackUrl" : "@{listCallbackUrl()}",
  		"Name" : "@{workflow().name}",
  		"workflowId" : "@{workflow().id}"
  	}
  }
  ```
- Build up the rest of the Logic App and then Enable it to have the HTTP WebHook send a subscription request to the service
- Post a message on the queue / subscription configured above and the Logic App should trigger almost immediately

## THANKS ##
Thanks to [Jeff Hollan](https://github.com/jeffhollan) for the great assistance in getting me started on the right path!

## TODO ##
- Automatically determine the current subscription
- Create an easier deployment process
- Unit tests
- Improve the tracing logs
- Thorough testing against various service bus configurations, e.g. partitioned queues, express queues, etc
