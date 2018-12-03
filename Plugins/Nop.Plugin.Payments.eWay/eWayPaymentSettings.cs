using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.eWay
{
    public class eWayPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public string CustomerId { get; set; }
        public decimal AdditionalFee { get; set; }
    }
}
