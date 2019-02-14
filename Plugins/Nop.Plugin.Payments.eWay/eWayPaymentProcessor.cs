using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.eWay.Models;
using Nop.Plugin.Payments.eWay.Validators;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.eWay
{
    /// <summary>
    /// eWay payment processor
    /// </summary>
    public class eWayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private const string APPROVED_RESPONSE = "00";
        private const string HONOUR_RESPONSE = "08";

        private readonly ICustomerService _customerService;
        private readonly eWayPaymentSettings _eWayPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public eWayPaymentProcessor(ICustomerService customerService, 
            eWayPaymentSettings eWayPaymentSettings,
            ISettingService settingService, 
            IStoreContext storeContext,
            ILocalizationService localizationService, 
            IWebHelper webHelper)
        {
            this._customerService = customerService;
            this._eWayPaymentSettings = eWayPaymentSettings;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._localizationService = localizationService;
            this._webHelper = webHelper;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets eWay URL
        /// </summary>
        /// <returns></returns>
        private string GeteWayUrl()
        {
            return _eWayPaymentSettings.UseSandbox ? "https://www.eway.com.au/gateway_cvn/xmltest/TestPage.asp" :
                "https://www.eway.com.au/gateway_cvn/xmlpayment.asp";
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
            var result = new ProcessPaymentResult();

            var eWaygateway = new GatewayConnector();

            var eWayRequest = new GatewayRequest
            {
                EwayCustomerID = _eWayPaymentSettings.CustomerId,
                CardNumber = processPaymentRequest.CreditCardNumber,
                CardExpiryMonth = processPaymentRequest.CreditCardExpireMonth.ToString("D2"),
                CardExpiryYear = processPaymentRequest.CreditCardExpireYear.ToString(),
                CardHolderName = processPaymentRequest.CreditCardName,
                InvoiceAmount = Convert.ToInt32(processPaymentRequest.OrderTotal * 100)
            };

            //Integer

            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
            var billingAddress = customer.BillingAddress;
            eWayRequest.PurchaserFirstName = billingAddress.FirstName;
            eWayRequest.PurchaserLastName = billingAddress.LastName;
            eWayRequest.PurchaserEmailAddress = billingAddress.Email;
            eWayRequest.PurchaserAddress = billingAddress.Address1;
            eWayRequest.PurchaserPostalCode = billingAddress.ZipPostalCode;
            eWayRequest.InvoiceReference = processPaymentRequest.OrderGuid.ToString();
            eWayRequest.InvoiceDescription = _storeContext.CurrentStore.Name + ". Order #" + processPaymentRequest.OrderGuid;
            eWayRequest.TransactionNumber = processPaymentRequest.OrderGuid.ToString();
            eWayRequest.CVN = processPaymentRequest.CreditCardCvv2;
            eWayRequest.EwayOption1 = string.Empty;
            eWayRequest.EwayOption2 = string.Empty;
            eWayRequest.EwayOption3 = string.Empty;

            // Do the payment, send XML doc containing information gathered
            eWaygateway.Uri = GeteWayUrl();
            var eWayResponse = eWaygateway.ProcessRequest(eWayRequest);
            if (eWayResponse != null)
            {
                // Payment succeeded get values returned
                if (eWayResponse.Status && (eWayResponse.Error.StartsWith(APPROVED_RESPONSE) || eWayResponse.Error.StartsWith(HONOUR_RESPONSE)))
                {
                    result.AuthorizationTransactionCode = eWayResponse.AuthorisationCode;
                    result.AuthorizationTransactionResult = eWayResponse.InvoiceReference;
                    result.AuthorizationTransactionId = eWayResponse.TransactionNumber;
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    //processPaymentResult.AuthorizationDate = DateTime.UtcNow;
                }
                else
                {
                    result.AddError("An invalid response was recieved from the payment gateway." + eWayResponse.Error);
                    //full error: eWAYRequest.ToXml().ToString()
                }
            }
            else
            {
                // invalid response recieved from server.
                result.AddError("An invalid response was recieved from the payment gateway.");
                //full error: eWAYRequest.ToXml().ToString()
            }

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
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel()
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
            };
            var validationResult = validator.Validate(model);
            if (validationResult.IsValid) return warnings;

            warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));
            return warnings;
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest
            {
                CreditCardType = form["CreditCardType"],
                CreditCardName = form["CardholderName"],
                CreditCardNumber = form["CardNumber"],
                CreditCardExpireMonth = int.Parse(form["ExpireMonth"]),
                CreditCardExpireYear = int.Parse(form["ExpireYear"]),
                CreditCardCvv2 = form["CardCode"]
            };

            return paymentInfo;
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            var settings = new eWayPaymentSettings()
            {
                UseSandbox = true,
                CustomerId = string.Empty,
                AdditionalFee = 0,
            };
            _settingService.SaveSetting(settings);

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.UseSandbox", "Use sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.UseSandbox.Hint", "Use sandbox?");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.CustomerId", "Customer ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.CustomerId.Hint", "Enter customer ID.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee", "Additional fee");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.eWay.PaymentMethodDescription", "Pay by credit / debit card");
            
            base.Install();
        }
        
        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.eWay.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.eWay.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.eWay.CustomerId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.eWay.CustomerId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.eWay.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.eWay.PaymentMethodDescription");
            
            base.Uninstall();
        }

        /// <summary>
        /// Gets a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <param name="viewComponentName">View component name</param>
        public string GetPublicViewComponentName()
        {
            return "PaymenteWay";
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            get { return _localizationService.GetResource("Plugins.Payments.eWay.PaymentMethodDescription"); }
        }

        #endregion
    }
}
