using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Paystack
{
    /// <summary>
    /// Represents settings of the Paystack Standard payment plugin
    /// </summary>
    public class PaystackPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets a value indicating the merchant secret key
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool EnableAdditionalFee { get; set; }
    }
}
