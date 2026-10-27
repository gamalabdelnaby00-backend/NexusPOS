using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UniversalPOS.Application.Sales;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Desktop
{
    public partial class PaymentDialog : Window
    {
        private readonly RegisterPaymentService _registerPaymentService;
        private readonly int _saleId;
        private readonly decimal _invoiceTotal;
        private readonly decimal _paidAmount;

        public PaymentDialog(int saleId, decimal invoiceTotal, decimal paidAmount, RegisterPaymentService registerPaymentService)
        {
            InitializeComponent();

            _saleId = saleId;
            _invoiceTotal = invoiceTotal;
            _paidAmount = paidAmount;
            _registerPaymentService = registerPaymentService;

            InvoiceNumberTextBlock.Text = $"Invoice #{saleId}";
            InvoiceTotalTextBlock.Text = $"{invoiceTotal:N2} EGP";

            PrimaryPaymentMethodComboBox.ItemsSource = Enum.GetValues<PaymentMethod>();
            SecondaryPaymentMethodComboBox.ItemsSource = Enum.GetValues<PaymentMethod>();
            PrimaryPaymentMethodComboBox.SelectionChanged += PrimaryPaymentMethodComboBox_SelectionChanged;

            UpdatePaymentInstructions();

            PrimaryPaymentMethodComboBox.SelectedItem = PaymentMethod.Cash;
            SecondaryPaymentMethodComboBox.SelectedItem = PaymentMethod.InstaPay;

            PrimaryPaymentAmountTextBox.Text = OutstandingBalance.ToString("F2");
            SecondaryPaymentAmountTextBox.Text = "0.00";

            PrimaryPaymentAmountTextBox.TextChanged += PaymentAmountTextBox_TextChanged;
            SecondaryPaymentAmountTextBox.TextChanged += PaymentAmountTextBox_TextChanged;

            UpdateBalance();
        }

        private decimal OutstandingBalance => _invoiceTotal - _paidAmount;

        private void EnableSplitPaymentCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (SecondaryPaymentPanel is null)
                return;

            bool isSplitPayment = EnableSplitPaymentCheckBox.IsChecked == true;

            SecondaryPaymentPanel.Visibility = isSplitPayment ? Visibility.Visible : Visibility.Collapsed;

            if (isSplitPayment)
            {
                decimal firstAmount = Math.Round(OutstandingBalance / 2, 2);
                PrimaryPaymentAmountTextBox.Text = firstAmount.ToString("F2");
                SecondaryPaymentAmountTextBox.Text = (OutstandingBalance - firstAmount).ToString("F2");
            }
            else
            {
                PrimaryPaymentAmountTextBox.Text = OutstandingBalance.ToString("F2");
                SecondaryPaymentAmountTextBox.Text = "0.00";
            }

            UpdateBalance();
        }

        private void PaymentAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBalance();
        }

        private void UpdateBalance()
        {
            if (OutstandingBalanceTextBlock is null)
                return;

            decimal firstAmount = ParseAmount(PrimaryPaymentAmountTextBox.Text);
            decimal secondAmount = EnableSplitPaymentCheckBox.IsChecked == true ? ParseAmount(SecondaryPaymentAmountTextBox.Text) : 0m;

            decimal remaining = OutstandingBalance - firstAmount - secondAmount;

            OutstandingBalanceTextBlock.Text = $"{remaining:N2} EGP";
            OutstandingBalanceTextBlock.Foreground = remaining < 0 ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.DarkSlateBlue;

            PaymentValidationTextBlock.Text = remaining < 0 ? "Amount exceeds balance." : string.Empty;
        }

        private static decimal ParseAmount(string? text)
        {
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) ? amount : 0m;
        }

        private async void ConfirmPaymentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (OutstandingBalance <= 0)
                    throw new InvalidOperationException("This invoice has no outstanding balance.");

                if (PrimaryPaymentMethodComboBox.SelectedItem is not PaymentMethod primaryMethod)
                    throw new InvalidOperationException("Select the primary payment method.");

                decimal primaryAmount = ParseAmount(PrimaryPaymentAmountTextBox.Text);

                string? primaryWalletProvider = primaryMethod == PaymentMethod.EWallet ? GetSelectedWalletProvider(): null;

                if (primaryMethod == PaymentMethod.EWallet && string.IsNullOrWhiteSpace(primaryWalletProvider))
                {
                    throw new InvalidOperationException("Please select a wallet provider.");
                }


                if (primaryAmount <= 0)
                    throw new InvalidOperationException("Primary payment amount must be greater than zero.");

                if (primaryAmount > OutstandingBalance)
                    throw new InvalidOperationException("Payment exceeds the outstanding balance.");

                ConfirmPaymentButton.IsEnabled = false;

                if (EnableSplitPaymentCheckBox.IsChecked == true)
                {
                    if (SecondaryPaymentMethodComboBox.SelectedItem is not PaymentMethod secondaryMethod)
                        throw new InvalidOperationException("Select the secondary payment method.");

                    if (primaryMethod == secondaryMethod)
                        throw new InvalidOperationException("Select two different payment methods.");

                    decimal secondaryAmount = ParseAmount(SecondaryPaymentAmountTextBox.Text);

                    string? secondaryWalletProvider = secondaryMethod == PaymentMethod.EWallet ? GetSelectedWalletProvider() : null;

                    if (secondaryMethod == PaymentMethod.EWallet && string.IsNullOrWhiteSpace(secondaryWalletProvider))
                    {
                        throw new InvalidOperationException("Please select a wallet provider.");
                    }


                    if (secondaryAmount <= 0)
                        throw new InvalidOperationException("Secondary payment amount must be greater than zero.");

                    if (primaryAmount + secondaryAmount > OutstandingBalance)
                        throw new InvalidOperationException("Total payment exceeds the outstanding balance.");

                    await _registerPaymentService.ExecuteSplitPaymentAsync(_saleId, primaryAmount, primaryMethod, secondaryAmount, secondaryMethod, primaryWalletProvider, secondaryWalletProvider);
                }
                else
                {
                    await _registerPaymentService.ExecuteAsync(_saleId, primaryAmount, primaryMethod, primaryWalletProvider);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Payment Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                ConfirmPaymentButton.IsEnabled = true;
            }
        }
        private void PrimaryPaymentMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePaymentInstructions();
        }


        private void UpdatePaymentInstructions()
        {
            if (PrimaryPaymentMethodComboBox.SelectedItem is not PaymentMethod method)
                return;

            // Show wallet providers only for EWallet
            WalletProviderPanel.Visibility = method == PaymentMethod.EWallet ? Visibility.Visible : Visibility.Collapsed;

            // Load the selected payment method logo
            string? imagePath = method switch
            {
                PaymentMethod.Cash => "Images/Cash.png",
                PaymentMethod.Card => "Images/Card.png",
                PaymentMethod.InstaPay => "Images/InstaPay.png",
                PaymentMethod.EWallet => "Images/EWallet.png",
                _ => null
            };

            if (imagePath is null)
            {
                PaymentLogo.Source = null;
            }
            else
            {
                var imageUri = new Uri($"/UniversalPOS.Desktop;component/{imagePath}", UriKind.Relative);

                PaymentLogo.Source = new System.Windows.Media.Imaging.BitmapImage(imageUri);
            }

            // Update payment instructions
            switch (method)
            {
                case PaymentMethod.Cash:
                    PaymentInstructionsTitle.Text = "Cash Payment";
                    PaymentInstructionsText.Text =
                        "Collect the cash from the customer, then confirm the payment.";
                    break;

                case PaymentMethod.Card:
                    PaymentInstructionsTitle.Text = "Card Payment";
                    PaymentInstructionsText.Text =
                        "1. Enter the invoice amount on the POS terminal.\n" +
                        "2. Let the customer use their card.\n" +
                        "3. Verify that the transaction succeeded before confirming here.";
                    break;

                case PaymentMethod.InstaPay:
                    PaymentInstructionsTitle.Text = "InstaPay Transfer";
                    PaymentInstructionsText.Text =
                        "1. Show the store's verified payment details or QR code.\n" +
                        "2. The customer completes the transfer.\n" +
                        "3. Verify receipt before confirming payment.";
                    break;

                case PaymentMethod.EWallet:
                    PaymentInstructionsTitle.Text = "Electronic Wallet";
                    PaymentInstructionsText.Text =
                        "1. Select the customer's wallet provider below.\n" +
                        "2. Follow the provider's payment instructions.\n" +
                        "3. Verify receipt before confirming payment.";
                    break;
            }
        }
        private string? GetSelectedWalletProvider()
        {
            if (WalletProviderComboBox.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString();

            return null;
        }

    }
}
