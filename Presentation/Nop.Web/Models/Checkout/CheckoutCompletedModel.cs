using Nop.Web.Framework.Models;
using System;

namespace Nop.Web.Models.Checkout
{
    public partial class CheckoutCompletedModel : BaseNopModel
    {
        public int OrderId { get; set; }
        public string CustomOrderNumber { get; set; }
        public bool OnePageCheckoutEnabled { get; set; }
        public string CustomerEmail { get; set; }
        public DateTime EstimatedDeliveryDate { get; set; }
    }
}