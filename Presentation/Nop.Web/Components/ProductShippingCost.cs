using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nop.Web.Components
{
    public class ProductShippingCostViewComponent: NopViewComponent
    {
        private readonly ICheckoutModelFactory _checkoutModelFactory;
        private readonly IProductService _productService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IGenericAttributeService _genericAttributeService;

        public ProductShippingCostViewComponent(IWorkContext workContext,
            IGenericAttributeService genericAttributeService,
            IProductService productService,
            ICheckoutModelFactory checkoutModelFactory,
            IStoreContext storeContext)
        {
            this._productService = productService;
            this._checkoutModelFactory = checkoutModelFactory;
            this._workContext = workContext;
            this._storeContext = storeContext;
            this._genericAttributeService = genericAttributeService;
        }

        public IViewComponentResult Invoke(int productId)
        {
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                 .Where(x => x.ShoppingCartTypeId == 1) //1 =shopping cart, 2=whishlist
                 // .LimitPerStore(_storeContext.CurrentStore.Id)
                 .ToList();
            var product = _productService.GetProductById(productId);
            if (product == null)
                //no product found
                return null;

            ShoppingCartItem item = new ShoppingCartItem()
            {
                Product = product,
                Customer = _workContext.CurrentCustomer
            };
            cart.Add(item);
            // if (cart != null && cart.Count > 0)
            // {
            var model = _checkoutModelFactory.PrepareShippingMethodModel(cart, _workContext.CurrentCustomer.ShippingAddress != null ? _workContext.CurrentCustomer.ShippingAddress : new Core.Domain.Common.Address());

            var selectedShippingOption = _genericAttributeService.GetAttribute<ShippingOption>(_workContext.CurrentCustomer,
                       NopCustomerDefaults.SelectedShippingOptionAttribute, _storeContext.CurrentStore.Id);
            if (selectedShippingOption != null)
            {
                var shippingOptionToSelect = model.ShippingMethods.ToList()
                    .Find(so =>
                       !string.IsNullOrEmpty(so.Name) &&
                       so.Name.Equals(selectedShippingOption.Name, StringComparison.InvariantCultureIgnoreCase) &&
                       !string.IsNullOrEmpty(so.ShippingRateComputationMethodSystemName) &&
                       so.ShippingRateComputationMethodSystemName.Equals(selectedShippingOption.ShippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase));
                if (shippingOptionToSelect != null)
                {
                    shippingOptionToSelect.Selected = true;
                }
            }

            // var model = _productService.PrepareAdminHeaderLinksModel();
            return View(model);
            //}
        }
    }
}
