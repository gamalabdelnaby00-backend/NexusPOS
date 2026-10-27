
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities
{
    public class Payment
    {
        public int Id { get; private set; }

        public decimal Amount { get; private set; }

        public PaymentMethod Method { get; private set; }

        public string? WalletProvider { get; private set; }

        public Payment(decimal amount, PaymentMethod method, string? walletProvider = null)
        {
            if (amount <= 0)
                throw new ArgumentException("Payment amount must be greater than zero.");

            if (!Enum.IsDefined(method))
                throw new ArgumentException("Invalid payment method.");

            if (method == PaymentMethod.EWallet && string.IsNullOrWhiteSpace(walletProvider))
            {
                throw new ArgumentException("Wallet provider is required for electronic wallet payments.");
            }

            if (method != PaymentMethod.EWallet && !string.IsNullOrWhiteSpace(walletProvider))
            {
                throw new ArgumentException("Wallet provider can only be specified for electronic wallet payments.");
            }

            Amount = amount;
            Method = method;
            WalletProvider = string.IsNullOrWhiteSpace(walletProvider) ? null : walletProvider.Trim();
        }
    }
}
