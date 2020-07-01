using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayStack.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using PayStack.Net;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace Nop.Plugin.Payments.PayStack.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PaymentPayStackController : BasePaymentController
    {
        #region Fields

        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;

        #endregion

        #region Ctor
        public PaymentPayStackController(
          IOrderProcessingService orderProcessingService,
          IOrderService orderService,
          IPermissionService permissionService,
          ILocalizationService localizationService,
          INotificationService notificationService,
          ISettingService settingService,
          IStoreContext storeContext,
          IWebHelper webHelper,
          IWorkContext workContext)
        {
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _permissionService = permissionService;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
        }
        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            int storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var payStackPaymentSettings = _settingService.LoadSetting<PayStackPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                SecretKey = payStackPaymentSettings.SecretKey,
                AdditionalFee = payStackPaymentSettings.AdditionalFee,
                EnableAdditionalFee = payStackPaymentSettings.EnableAdditionalFee,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope <= 0)
                return View("~/Plugins/Payments.PayStack/Views/Configure.cshtml", model);

            model.SecretKey_OverrideForStore = _settingService.SettingExists(payStackPaymentSettings, x => x.SecretKey, storeScope);
            model.AdditionalFee_OverrideForStore = _settingService.SettingExists(payStackPaymentSettings, x => x.AdditionalFee, storeScope);
            model.EnableAdditionalFee_OverrideForStore = _settingService.SettingExists(payStackPaymentSettings, x => x.EnableAdditionalFee, storeScope);
            return View("~/Plugins/Payments.PayStack/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            // load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var payStackPaymentSettings = _settingService.LoadSetting<PayStackPaymentSettings>(storeScope);

            payStackPaymentSettings.SecretKey = model.SecretKey;
            payStackPaymentSettings.AdditionalFee = model.AdditionalFee;
            payStackPaymentSettings.EnableAdditionalFee = model.EnableAdditionalFee;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(payStackPaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payStackPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payStackPaymentSettings, x => x.EnableAdditionalFee, model.EnableAdditionalFee_OverrideForStore, storeScope, false);
            
            //now clear settings cache
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        public IActionResult Callback()
        {
            try
            {
                PayStackPaymentSettings standardPaymentSettings = _settingService.LoadSetting<PayStackPaymentSettings>(_storeContext.ActiveStoreScopeConfiguration);

                string str = Request.QueryString.ToString();
                NameValueCollection queryString = HttpUtility.ParseQueryString(str);
                TransactionVerifyResponse transactionVerifyResponse = new PayStackApi(standardPaymentSettings.SecretKey).Transactions.Verify(queryString.Get("reference"));

                if (transactionVerifyResponse.Status && transactionVerifyResponse.Data.Status == "success")
                {
                    Metadata metadata = transactionVerifyResponse.Data.Metadata;
                    string empty1 = string.Empty;
                    string empty2 = string.Empty;
                    foreach (KeyValuePair<string, object> keyValuePair in (Dictionary<string, object>)metadata)
                    {
                        string key = keyValuePair.Key;
                        if (keyValuePair.Key == "customOrderNumber")
                            empty2 = keyValuePair.Value.ToString();
                        if (keyValuePair.Key == "orderGuid")
                            empty1 = keyValuePair.Value.ToString();
                    }

                    Order orderByGuid = _orderService.GetOrderByGuid(Guid.Parse(empty1));

                    if (this._orderProcessingService.CanMarkOrderAsPaid(orderByGuid))
                    {
                        orderByGuid.AuthorizationTransactionId = transactionVerifyResponse.Data.Reference;
                        this._orderService.UpdateOrder(orderByGuid);
                        this._orderProcessingService.MarkOrderAsPaid(orderByGuid);
                    }

                    return (IActionResult)((ControllerBase)this).RedirectToRoute("CheckoutCompleted", (object)new
                    {
                        orderId = ((BaseEntity)orderByGuid).Id
                    });
                }
                else if (transactionVerifyResponse.Status && transactionVerifyResponse.Data.Status == "failed" || (!transactionVerifyResponse.Status || !(transactionVerifyResponse.Data.Status == "abandoned")))
                {
                    var order = _orderService.SearchOrders(_storeContext.CurrentStore.Id,
                        customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
                    return RedirectToRoute("OrderDetails", new { orderId = order.Id });
                }
    
                return (IActionResult)((ControllerBase)this).Content(string.Empty);
            }
            catch (Exception ex)
            {
                return (IActionResult)((ControllerBase)this).Content(string.Empty);
            }
        }

        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(_storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();

            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("Homepage");
        }

        #endregion
    }
}
