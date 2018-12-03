namespace Nop.Plugin.Payments.eWay
{
    /// <summary>
    /// Summary description for GatewayRequest.
    /// Copyright Web Active Corporation Pty Ltd  - All rights reserved. 1998-2004
    /// This code is for exclusive use with the eWAY payment gateway
    /// </summary>
    public class GatewayRequest
    {
        /// <summary>
        /// Gets or sets an invoice amount
        /// </summary>
        public int InvoiceAmount { get; set; }

        /// <summary>
        /// Gets or sets the secured card data
        /// </summary>
        public string SecuredCardData { get; set; } = "";
    }
}
