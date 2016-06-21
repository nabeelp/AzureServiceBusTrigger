using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace AzureServiceBusTrigger
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private static string _subscriptionId;

        public static string SubscriptionId
        {
            get { return _subscriptionId; }
            set { _subscriptionId = value; }
        }

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);

            // get this site's subscription id
            SubscriptionId = ConfigurationManager.AppSettings["CurrentSubscriptionID"];
        }
    }
}
