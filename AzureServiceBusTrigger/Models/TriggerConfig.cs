using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;

namespace AzureServiceBusTrigger.Models
{
    public class TriggerConfig : IValidatableObject
    {
        public string ServiceBusConnectionString { get; set; }
        public string QueueName { get; set; }
        public string TopicName { get; set; }
        public string SubscriptionName { get; set; }

        /// <summary>
        /// Perform custom validation on the model
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // get the service bus connection string either from supplied parameter or from config
            string connectionString = String.Empty;
            if (String.IsNullOrEmpty(ServiceBusConnectionString))
            {
                connectionString = ConfigurationManager.AppSettings["MyServiceBusConnectionString"];
            }
            else
            {
                connectionString = ServiceBusConnectionString;
            }

            // if the service bus connection string is empty throw an error
            if (String.IsNullOrEmpty(connectionString))
                yield return new ValidationResult("No service bus connection string supplied, and no connection string with the key 'MyServiceBusConnectionString' found in the configuration settings", new[] { nameof(ServiceBusConnectionString) });

            // if there are no values for queue, topic or subscription
            if (String.IsNullOrEmpty(QueueName) && String.IsNullOrEmpty(TopicName) && String.IsNullOrEmpty(SubscriptionName))
                yield return new ValidationResult("Please supply values for the queue, or for the topic and subscription, to which this API needs to listen", new[] { nameof(QueueName) });
            else
            {
                // if there is a queue name, then there should not be a topic or subscription
                if (String.IsNullOrEmpty(QueueName) == false && (String.IsNullOrEmpty(TopicName) == false || String.IsNullOrEmpty(SubscriptionName) == false))
                    yield return new ValidationResult("If listening on a queue, both the TopicName and the SubscriptionName should be empty or omitted", new[] { nameof(QueueName) });

                // if there is a no queue name, then there should be a topic AND a subscription
                if (String.IsNullOrEmpty(QueueName) && (String.IsNullOrEmpty(TopicName) || String.IsNullOrEmpty(SubscriptionName)))
                    yield return new ValidationResult("If listening on a topic and subscription, both the TopicName and the SubscriptionName should be provided", new[] { nameof(TopicName) });
            }
        }
    }
}