using System;
using System.Collections.Generic;
using eWAY.Rapid.Enums;
using eWAY.Rapid.Models;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Payments;
using StackExchange.Profiling.Helpers;
using CapturePaymentRequest = Nop.Services.Payments.CapturePaymentRequest;

namespace Nop.Plugin.Payments.eWay
{
    /// <summary>
    /// eWay payment processor
    /// </summary>
    public class eWayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly eWayPaymentSettings _eWayPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public eWayPaymentProcessor(ICustomerService customerService, 
            eWayPaymentSettings eWayPaymentSettings,
            ISettingService settingService, 
            IWebHelper webHelper, 
            ILogger logger)
        {
            this._customerService = customerService;
            this._eWayPaymentSettings = eWayPaymentSettings;
            this._settingService = settingService;
            this._webHelper = webHelper;
            _logger = logger;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets eWay URL
        /// </summary>
        /// <returns></returns>
        private string GetRapidEndpoint()
            => _eWayPaymentSettings.UseSandbox ? "Sandbox" : "Production";

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            var eWaygateway = new GatewayConnector();

            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
            var billingAddress = customer.BillingAddress;

            var eWayRequest = new Transaction
            {
                Customer = new Customer
                {
                    CompanyName = billingAddress.Company,
                    FirstName = billingAddress.FirstName,
                    LastName = billingAddress.LastName,
                    Email = billingAddress.Email,
                    Address = new Address
                    {
                        Street1 = billingAddress.Address1,
                        Street2 = billingAddress.Address2,
                        City = billingAddress.City,
                        Country = billingAddress.Country?.TwoLetterIsoCode?.ToLower(),
                        PostalCode = billingAddress.ZipPostalCode
                    },
                    Reference = customer.CustomerGuid.ToString()
                },
                PaymentDetails = new PaymentDetails
                {
                    CurrencyCode = "NZD",
                    InvoiceReference = processPaymentRequest.OrderGuid.ToString(),
                    InvoiceNumber = processPaymentRequest.OrderGuid.ToString(),
                    TotalAmount = Convert.ToInt32(processPaymentRequest.OrderTotal * 100)
                },
                TransactionType = TransactionTypes.Purchase,
                SecuredCardData = processPaymentRequest.CreditCardName
            };

            // Do the payment
            eWaygateway.RapidEndpoint = GetRapidEndpoint();
            var eWayResponse = eWaygateway.ProcessRequest(eWayRequest);
            if (eWayResponse == null ||
                eWayResponse.Errors != null ||
                !(eWayResponse.TransactionStatus?.Status ?? false) ||
                !eWayResponse.TransactionStatus.ProcessingDetails.ResponseMessage.StartsWith("A"))
            {
                // invalid response recieved from server.
                result.AddError("An error occurred while processing your order.  Please check your Credit Card details and try again.");
                _logger.Error($"Error recieved from eWay: [Error: {eWayResponse?.Errors?.ToJson()}] [ResponseMessage: {eWayResponse?.TransactionStatus?.ToJson()}]");

                return result;
            }

            _logger.Information($"Successful eWay payment with result [{eWayResponse.TransactionStatus?.ToJson()}]");

            result.NewPaymentStatus = PaymentStatus.Paid;

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //nothing
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
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
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _eWayPaymentSettings.AdditionalFee;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
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
            
            //it's not a redirection payment method. So we always return false
            return false;
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymenteWay/Configure";
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
            return new ProcessPaymentRequest
            {
                CreditCardName = form["Securefieldcode"]
            };
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            var settings = new eWayPaymentSettings
            {
                UseSandbox = true,
                CustomerId = string.Empty,
                AdditionalFee = 0,
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.UseSandbox", "Use sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.UseSandbox.Hint", "Use sandbox?");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.CustomerId", "Customer ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.CustomerId.Hint", "Enter customer ID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            
            base.Install();
        }
        
        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.eWay.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.eWay.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.eWay.CustomerId");
            this.DeletePluginLocaleResource("Plugins.Payments.eWay.CustomerId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee.Hint");

            base.Uninstall();
        }

        /// <summary>
        /// Gets a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <param name="viewComponentName">View component name</param>
        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "PaymenteWay";
        }

        #endregion

        #region Properies

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
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => string.Empty;

        #endregion
    }
}
