using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.eWay.Models;
using Nop.Services.Configuration;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.eWay.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class PaymenteWayController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly eWayPaymentSettings _eWayPaymentSettings;
        private readonly IPermissionService _permissionService;

        public PaymenteWayController(ISettingService settingService, 
            eWayPaymentSettings eWayPaymentSettings,
            IPermissionService permissionService)
        {
            this._settingService = settingService;
            this._eWayPaymentSettings = eWayPaymentSettings;
            this._permissionService = permissionService;
        }

        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                UseSandbox = _eWayPaymentSettings.UseSandbox,
                CustomerId = _eWayPaymentSettings.CustomerId,
                AdditionalFee = _eWayPaymentSettings.AdditionalFee
            };

            return View("~/Plugins/Payments.eWay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _eWayPaymentSettings.UseSandbox = model.UseSandbox;
            _eWayPaymentSettings.CustomerId = model.CustomerId;
            _eWayPaymentSettings.AdditionalFee = model.AdditionalFee;
            _settingService.SaveSetting(_eWayPaymentSettings);

            return Configure();
        }
    }
}