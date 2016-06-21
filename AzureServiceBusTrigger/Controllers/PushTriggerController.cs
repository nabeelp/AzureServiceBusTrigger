using Newtonsoft.Json.Linq;
using AzureServiceBusTrigger.Helpers;
using AzureServiceBusTrigger.Models;
using Swashbuckle.Swagger.Annotations;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using AzureServiceBusTrigger;
using System;

namespace AzureServiceBusTrigger.Controllers
{
    [RoutePrefix("trigger/push")]
    public class PushTriggerController : ApiController
    {
        /// <summary>
        /// Method called when a Logic App wants to create or update a subscription to the push trigger
        /// </summary>
        /// <param name="subscriptionInfo">The configuration values to be used in creating the push trigger subscription</param>
        /// <returns>Result of registering the callback URL for this push trigger subscription</returns>
        // POST trigger/push/subscribe
        [HttpPost, Route("subscribe")]
        public async Task<HttpResponseMessage> Subscribe([FromBody]SubscriptionInfo subscriptionInfo)
        {
            if (ModelState.IsValid)
            {
                // if we need to ensure we limit subscription to the current subscription, check that first
                if (ConfigurationManager.AppSettings["LimitToCurrentSubscription"].ToLower() == "true")
                {
                    // get the incoming subscription info from the workflow id, e.g. "/subscriptions/2363d4bc-db48-46e8-b725-3810f7bb24a4/resourceGroups/..."
                    string incomingSubscriptionId = subscriptionInfo.logicAppInfo.WorkflowId.Split(new char[] { '/' })[2];
                    if (WebApiApplication.SubscriptionId != incomingSubscriptionId)
                    {
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Request cannot be honored, as it is not coming from the correct subscription.");
                    }
                }

                // check if theree is already an entry for this workflow, if so ... unregister it first, since it may have the old URL
                if (InMemoryTriggerStore.Instance.GetStore().ContainsKey(subscriptionInfo.logicAppInfo.WorkflowId))
                {
                    await InMemoryTriggerStore.Instance.UnregisterTrigger(subscriptionInfo.logicAppInfo.WorkflowId);
                }

                // Register the trigger ... this will do an upsert
                Trace.TraceInformation("Register Push Trigger - " + subscriptionInfo.logicAppInfo.WorkflowId);
                try
                {
                    await InMemoryTriggerStore.Instance.RegisterTrigger(subscriptionInfo.logicAppInfo.WorkflowId, subscriptionInfo);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error occurred during subscribe: " + ex.ToString());
                    return Request.CreateErrorResponse(HttpStatusCode.ExpectationFailed, ex);
                }

                // Notify the Logic App that the callback was registered
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            else
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ModelState);
            }
        }

        /// <summary>
        /// Method called when a Logic App wants to remove a subscription to the push trigger
        /// </summary>
        /// <param name="logicAppId">The unique identifier for the logic app, acquired in the Logic App using workflow().id</param>
        /// <returns>An error response to indicate if the trigger could not be found, or an OK if the trigger was found and was removed</returns>
        // DELETE trigger/push/unsubscribe
        [SwaggerResponse(HttpStatusCode.NotFound, "The logic app had no callback registered")]
        [HttpPost, Route("unsubscribe")]
        public async Task<HttpResponseMessage> Unsubscribe([FromBody]LogicAppInfo inputs)
        {
            Trace.TraceInformation("Unregister Callback - " + inputs.WorkflowId);
            if (!InMemoryTriggerStore.Instance.GetStore().ContainsKey(inputs.WorkflowId))
            {
                Trace.TraceInformation("No Callback found - " + inputs.WorkflowId);
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No subscription registered for " + inputs.WorkflowId);
            }

            // Remove the stored callback by logic app id
            try
            {
                await InMemoryTriggerStore.Instance.UnregisterTrigger(inputs.WorkflowId);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error occurred during unsubscribe: " + ex.ToString());
                return Request.CreateErrorResponse(HttpStatusCode.ExpectationFailed, ex);
            }

            // return a success result
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        /// <summary>
        /// Retrieves all Logic Apps awaiting push trigger callbacks
        /// </summary>
        /// <returns>A listing of the push trigger callbacks in the store</returns>
        [SwaggerResponse(HttpStatusCode.OK, "Indicates the operation completed without error", typeof(string))]
        [HttpGet, Route("all", Order = 0)]
        public async Task<HttpResponseMessage> RetrieveTheTriggers()
        {
            Trace.WriteLine("RetrieveTheTriggers called");

            try
            {
                return Request.CreateResponse<IDictionary<string, SubscriptionInfo>>(HttpStatusCode.OK, InMemoryTriggerStore.Instance.GetStore());
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.ExpectationFailed, ex);
            }
        }
    }

}
