using eWAY.Rapid;
using eWAY.Rapid.Enums;
using eWAY.Rapid.Models;

namespace Nop.Plugin.Payments.eWay
{
    /// <summary>
    /// Summary description for GatewayConnector.
    /// This code is for exclusive use with the eWAY payment gateway
    /// </summary>
    public class GatewayConnector
    {
        /// <summary>
        /// The RapidEndpoint of the Eway payment gateway
        /// </summary>
        public string RapidEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Do the post to the gateway and retrieve the response
        /// </summary>
        /// <param name="request">Request</param>
        /// <returns>Response</returns>
        public CreateTransactionResponse ProcessRequest(Transaction request)
        {
//            var ewayClient = RapidClientFactory.NewRapidClient("44DD7Cns/tsiCi4GiJSIaM5qGLNMZmdPZ9ADSKSt+ctDUx6SjkGzzi9DVLy4Uoh8GCZ99f", "ZlVMyKbt", RapidEndpoint);
            var ewayClient = RapidClientFactory.NewRapidClient("44DD7A3Z5yFY0uS6s+PeaUvVI6MrmoZF8BpNE2UY4lvDbbJOdzK0bj3zVLxkfhCH7FMAlo", "K4ReUOmQ", RapidEndpoint);

            return ewayClient.Create(PaymentMethod.Direct, request);
        }
    }
}


