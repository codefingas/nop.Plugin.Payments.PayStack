using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Paystack.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="endpointRouteBuilder">Route builder</param>
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //Paystack Callback
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Paystack.Callback", "Plugins/PaymentPaystack/Callback",
                 new { controller = "PaymentPaystack", action = "Callback" });

            //Cancel
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Paystack.CancelOrder", "Plugins/PaymentPaystack/CancelOrder",
                 new { controller = "PaymentPaystack", action = "CancelOrder" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => -1;
    }
}