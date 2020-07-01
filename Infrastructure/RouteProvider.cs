using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayStack.Infrastructure
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
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PayStack.Callback", "Plugins/PaymentPayStack/Callback",
                 new { controller = "PaymentPayStack", action = "Callback" });

            //Cancel
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PayStack.CancelOrder", "Plugins/PaymentPayStack/CancelOrder",
                 new { controller = "PaymentPayStack", action = "CancelOrder" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => -1;
    }
}