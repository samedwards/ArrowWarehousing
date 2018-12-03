﻿using Nop.Web.Framework.Mvc.Models;

namespace Nop.Web.Models.Checkout
{
    public partial class CheckoutCompletedModel : BaseNopModel
    {
        public int OrderId { get; set; }
        public string CustomOrderNumber { get; set; }
        public bool OnePageCheckoutEnabled { get; set; }
        public bool HasPaid { get; set; }
    }
}