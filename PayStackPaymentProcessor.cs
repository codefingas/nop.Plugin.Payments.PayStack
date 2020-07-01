using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using PayStack.Net;

namespace Nop.Plugin.Payments.Paystack
{
    /// <summary>
    /// PaystackPaymentProcessor payment processor
    /// </summary>
    public class PaystackPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly PaystackPaymentSettings _PaystackPaymentSettings;
        private readonly ICustomerService _customerService;

        #endregion

        #region Ctor

        public PaystackPaymentProcessor(CurrencySettings currencySettings,
          ICheckoutAttributeParser checkoutAttributeParser,
          ICurrencyService currencyService,
          IGenericAttributeService genericAttributeService,
          IHttpContextAccessor httpContextAccessor,
          ILocalizationService localizationService,
          IPaymentService paymentService,
          ISettingService settingService,
          IWebHelper webHelper,
          ICustomerService customerService,
          PaystackPaymentSettings PaystackPaymentSettings)
        {
            _currencySettings = currencySettings;
            _checkoutAttributeParser = checkoutAttributeParser;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _webHelper = webHelper;
            _PaystackPaymentSettings = PaystackPaymentSettings;
            _customerService = customerService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            string storeLocation = _webHelper.GetStoreLocation(new bool?());
            Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            PayStackApi PaystackApi = new PayStackApi(_PaystackPaymentSettings.SecretKey);
            Customer customerById = _customerService.GetCustomerById(postProcessPaymentRequest.Order.CustomerId);
            string empty = string.Empty;
            string customerEmail = customerById.Email != null || customerById == null ? customerById.Email : _customerService.GetCustomerShippingAddress(customerById).Email;

            TransactionInitializeRequest request = new TransactionInitializeRequest();
            request.AmountInKobo = Convert.ToInt32(Math.Ceiling(postProcessPaymentRequest.Order.OrderTotal)) * 100;
            request.Email = customerEmail;

            request.MetadataObject["CustomOrderNumber"] = (object)postProcessPaymentRequest.Order.CustomOrderNumber;
            Dictionary<string, object> metadataObject = request.MetadataObject;
            Guid orderGuid = postProcessPaymentRequest.Order.OrderGuid;

            string guidString = orderGuid.ToString();
            metadataObject["OrderGuid"] = (object)guidString;
            request.CallbackUrl = storeLocation + "Plugins/PaymentPaystack/Callback";
            TransactionInitializeRequest initializeRequest = request;
            orderGuid = postProcessPaymentRequest.Order.OrderGuid;

            guidString = orderGuid.ToString();
            initializeRequest.Reference = guidString;
            TransactionInitializeResponse initializeResponse = PaystackApi.Transactions.Initialize(request, true);

            if (!initializeResponse.Status)
                return;

            _httpContextAccessor.HttpContext.Response.Redirect(initializeResponse.Data.AuthorizationUrl);
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _PaystackPaymentSettings.EnableAdditionalFee ? _paymentService.CalculateAdditionalFee(cart, _PaystackPaymentSettings.AdditionalFee, true) : decimal.Zero;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return _webHelper.GetStoreLocation(new bool?()) + "Admin/PaymentPaystack/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentPaystack";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new PaystackPaymentSettings());

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.Fields.SecretKey", "Paystack Secret Key", (string)null);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.Fields.SecretKey.Hint", "Copy your secret key from your Paystack dashboard. This can be either the test secret key or live secret key.", (string)null);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.Fields.RedirectionTip", "For security purposes, you will be redirected to Paystack site to complete the order.", (string)null);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.Fields.AdditionalFee", "Paystack Percentage fee", (string)null);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.Fields.AdditionalFee.Hint", "Enter Paystack percentage fee to charge your customers. This is the percentage Paystack charges per transaction.", (string)null);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.Fields.EnableAdditionalFee", "Enable Additional fee", (string)null);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.Fields.EnableAdditionalFee.Hint", "Check this box if you want Paystack percentage charge to be calculated on checkout. Make sure you enter the percentage.", (string)null);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paystack.PaymentMethodDescription", "For security purposes, you will be redirected to Paystack site to complete the order.", (string)null);

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PaystackPaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.Fields.SecretKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.Fields.SecretKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.Fields.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.Fields.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.EnableAdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.EnableAdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paystack.PaymentMethodDescription");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => false;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => false;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => false;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => false;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.Paystack.PaymentMethodDescription");

        #endregion
    }
}