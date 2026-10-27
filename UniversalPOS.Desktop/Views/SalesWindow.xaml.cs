using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Sales;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Desktop;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Desktop.Views
{
    public partial class SalesWindow : Window
    {
        private readonly CreateSaleService _createSaleService;
        private readonly AddProductToSaleService _addProductToSaleService;
        private readonly GetProductsService _getProductsService;
        private readonly GetSaleDetailsService _getSaleDetailsService;
        private readonly RegisterPaymentService _registerPaymentService;
        private readonly CompleteSaleService _completeSaleService;
        private readonly ISaleRepository _saleRepository;

        private readonly List<Product> _allProducts = new();
        private readonly List<CartItemViewModel> _cartItems = new();

        private int? _currentSaleId;
        private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
        private bool _isBusy;

        public SalesWindow()
        {
            InitializeComponent();

            var services = App.Services;
            var currentUser = services.GetRequiredService<CurrentUserSession>();
            if (CashierNameText != null)
                CashierNameText.Text = currentUser.DisplayName
                    ?? currentUser.UserName
                    ?? "Unknown user";

            _createSaleService =
                services.GetRequiredService<CreateSaleService>();

            _addProductToSaleService =
                services.GetRequiredService<AddProductToSaleService>();

            _getProductsService =
                services.GetRequiredService<GetProductsService>();

            _getSaleDetailsService =
                services.GetRequiredService<GetSaleDetailsService>();

            _registerPaymentService =
                services.GetRequiredService<RegisterPaymentService>();

            _completeSaleService =
                services.GetRequiredService<CompleteSaleService>();

            _saleRepository =
                services.GetRequiredService<ISaleRepository>();

            Loaded += SalesWindow_Loaded;

            SelectPaymentMethod(PaymentMethod.Cash);
        }

        // =========================================================
        // INITIALIZATION
        // =========================================================

        private async void SalesWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await RunSafelyAsync(async () =>
            {
                // Restore the user's open shift (or offer to open one) before any sale can be created.
                if (!await ShiftFlow.EnsureOpenShiftAsync())
                {
                    Close();
                    return;
                }

                await LoadProductsAsync();
                await CreateNewSaleAsync();
            });
        }

        private async Task LoadProductsAsync()
        {
            var products = await _getProductsService.ExecuteAsync();

            _allProducts.Clear();

            _allProducts.AddRange(
                products.Where(p => p.IsActive));

            DisplayProducts(_allProducts);
        }

        private async Task CreateNewSaleAsync()
        {
            var saleId = await _createSaleService.ExecuteAsync();

            _currentSaleId = saleId;

            if (ReceiptNumberText != null)
            {
                ReceiptNumberText.Text =
                    $"Receipt #{saleId}";
            }

            _cartItems.Clear();

            if (CartItems != null)
                CartItems.ItemsSource = null;

            UpdateTotals(0m);
        }

        // =========================================================
        // PRODUCT DISPLAY AND SEARCH
        // =========================================================

        private void DisplayProducts(IEnumerable<Product> products)
        {
            var productCards = products.Select(p =>
                new ProductCardViewModel
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    Barcode = p.Barcode,
                    ImagePath = GetProductImagePath(p.Id)
                }).ToList();

            ProductsItems.ItemsSource = productCards;
        }

        private static string GetProductImagePath(int productId)
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Images",
                "Products",
                $"{productId}.png");
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(searchText))
            {
                DisplayProducts(_allProducts);
                return;
            }

            var filteredProducts = _allProducts.Where(p =>
                p.Name.Contains(
                    searchText,
                    StringComparison.OrdinalIgnoreCase)
                ||
                p.Barcode.Contains(
                    searchText,
                    StringComparison.OrdinalIgnoreCase));

            DisplayProducts(filteredProducts);
        }

        private void Category_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            var category = button.Tag?.ToString();

            if (category == "All" || string.IsNullOrWhiteSpace(category))
            {
                DisplayProducts(_allProducts);
                return;
            }

            // Product currently contains CategoryId but no CategoryName.
            // Therefore, category-name filtering must wait until the
            // category lookup is connected to this screen.
            MessageBox.Show(
                "تصنيف المنتجات بالاسم لم يتم ربطه بالشاشة حتى الآن.",
                "تصنيفات المنتجات",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // ADD PRODUCT TO CART
        // =========================================================

        private async void AddProduct_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not ProductCardViewModel product)
            {
                return;
            }

            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue)
                    await CreateNewSaleAsync();

                await _addProductToSaleService.ExecuteAsync(
                    _currentSaleId!.Value,
                    product.Barcode,
                    1m);

                await RefreshCartAsync();
            });
        }

        // =========================================================
        // CART REFRESH
        // =========================================================

        private async Task RefreshCartAsync()
        {
            if (!_currentSaleId.HasValue)
                return;

            var saleDetails =
                await _getSaleDetailsService.ExecuteAsync(
                    _currentSaleId.Value);

            _cartItems.Clear();

            foreach (var item in saleDetails.Items)
            {
                _cartItems.Add(new CartItemViewModel
                {
                    ProductId = item.ProductId,
                    Name = item.ProductName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    Total = item.TotalPrice,
                    ImagePath = GetProductImagePath(item.ProductId)
                });
            }

            CartItems.ItemsSource = null;
            CartItems.ItemsSource = _cartItems.ToList();

            UpdateTotals(saleDetails.TotalAmount);
        }

        private void UpdateTotals(decimal subtotal)
        {
            if (SubtotalText != null)
                SubtotalText.Text = subtotal.ToString("N2");

            // Tax and discounts are not yet part of the Sale domain
            // calculation, so they are shown as zero rather than
            // charging an amount that the payment service doesn't know about.
            if (VatText != null)
                VatText.Text = "0.00";

            if (DiscountText != null)
                DiscountText.Text = "0.00";

            if (TotalText != null)
                TotalText.Text = subtotal.ToString("N2");
        }

        // =========================================================
        // INCREASE / DECREASE QUANTITY
        // =========================================================

        private async void IncreaseQuantity_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not CartItemViewModel item)
            {
                return;
            }

            await ChangeQuantityAsync(item, item.Quantity + 1m);
        }

        private async void DecreaseQuantity_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not CartItemViewModel item)
            {
                return;
            }

            if (item.Quantity <= 1m)
            {
                MessageBox.Show(
                    "استخدم زر الحذف لإزالة المنتج من الفاتورة.",
                    "تعديل الكمية",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            await ChangeQuantityAsync(item, item.Quantity - 1m);
        }

        private async Task ChangeQuantityAsync(
            CartItemViewModel item,
            decimal newQuantity)
        {
            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue)
                    return;

                var sale = await _saleRepository.GetByIdAsync(
                    _currentSaleId.Value);

                if (sale == null)
                    throw new InvalidOperationException(
                        "الفاتورة غير موجودة.");

                var saleItem = sale.Items.FirstOrDefault(
                    x => x.ProductId == item.ProductId);

                if (saleItem == null)
                    throw new InvalidOperationException(
                        "المنتج غير موجود في الفاتورة.");

                sale.ChangeItemQuantity(saleItem, newQuantity);

                await _saleRepository.UpdateAsync(sale);

                await RefreshCartAsync();
            });
        }

        // =========================================================
        // REMOVE PRODUCT
        // =========================================================

        private async void RemoveCartItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not CartItemViewModel item)
            {
                return;
            }

            var answer = MessageBox.Show(
                $"هل تريد حذف {item.Name} من الفاتورة؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue)
                    return;

                var sale = await _saleRepository.GetByIdAsync(
                    _currentSaleId.Value);

                if (sale == null)
                    throw new InvalidOperationException(
                        "الفاتورة غير موجودة.");

                var saleItem = sale.Items.FirstOrDefault(
                    x => x.ProductId == item.ProductId);

                if (saleItem == null)
                    return;

                // Requires the RemoveItem method shown below this code.
                sale.RemoveItem(saleItem);

                await _saleRepository.UpdateAsync(sale);

                await RefreshCartAsync();
            });
        }

        // =========================================================
        // CUSTOMER
        // =========================================================

        private void SelectCustomer_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "سيتم ربط اختيار العملاء عند إضافة وحدة العملاء.",
                "العملاء",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // PAYMENT METHOD SELECTION
        // =========================================================

        private void PaymentMethod_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            var methodName = button.Tag?.ToString();

            if (!Enum.TryParse(
                    methodName,
                    true,
                    out PaymentMethod method))
            {
                MessageBox.Show(
                    "وسيلة الدفع غير معروفة.",
                    "الدفع",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SelectPaymentMethod(method);
        }

        private void SelectPaymentMethod(PaymentMethod method)
        {
            _selectedPaymentMethod = method;

            SetPaymentButtonStyle(CashButton, method == PaymentMethod.Cash);
            SetPaymentButtonStyle(CardButton, method == PaymentMethod.Card);
            SetPaymentButtonStyle(InstaPayButton, method == PaymentMethod.InstaPay);
            SetPaymentButtonStyle(EWalletButton, method == PaymentMethod.EWallet);

            if (AmountReceivedBox != null)
            {
                AmountReceivedBox.Text = "0.00";
            }
        }

        private static void SetPaymentButtonStyle(
            Button? button,
            bool selected)
        {
            if (button == null)
                return;

            button.BorderThickness = selected
                ? new Thickness(2)
                : new Thickness(1);

            button.BorderBrush = selected
                ? Brushes.DodgerBlue
                : Brushes.LightGray;

            button.FontWeight = selected
                ? FontWeights.Bold
                : FontWeights.Normal;
        }

        // =========================================================
        // SPLIT PAYMENT
        // =========================================================

        private async void SplitPayment_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue)
                    return;

                var details =
                    await _getSaleDetailsService.ExecuteAsync(
                        _currentSaleId.Value);

                if (details.Items.Count == 0)
                {
                    MessageBox.Show(
                        "أضف منتجات إلى الفاتورة أولاً.",
                        "الدفع المقسّم",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                var remaining = details.RemainingAmount;

                var amountText = Interaction.InputBox(
                    $"المتبقي: {remaining:N2}\n" +
                    "أدخل المبلغ الذي ستدفعه بوسيلة الدفع الأولى:",
                    "الدفع المقسّم",
                    (remaining / 2m).ToString(
                        "0.00",
                        CultureInfo.InvariantCulture));

                if (!decimal.TryParse(
                        amountText,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var firstAmount)
                    &&
                    !decimal.TryParse(
                        amountText,
                        NumberStyles.Number,
                        CultureInfo.CurrentCulture,
                        out firstAmount))
                {
                    return;
                }

                if (firstAmount <= 0 || firstAmount >= remaining)
                {
                    MessageBox.Show(
                        "المبلغ الأول يجب أن يكون أكبر من صفر وأقل من المتبقي.",
                        "الدفع المقسّم",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                var firstMethod = AskPaymentMethod(
                    "اختر وسيلة الدفع الأولى");

                if (!firstMethod.HasValue)
                    return;

                var secondMethod = AskPaymentMethod(
                    "اختر وسيلة الدفع الثانية");

                if (!secondMethod.HasValue)
                    return;

                if (firstMethod.Value == secondMethod.Value)
                {
                    MessageBox.Show(
                        "يجب اختيار وسيلتي دفع مختلفتين.",
                        "الدفع المقسّم",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string? firstProvider = null;
                string? secondProvider = null;

                if (firstMethod == PaymentMethod.EWallet)
                {
                    firstProvider = AskWalletProvider();

                    if (string.IsNullOrWhiteSpace(firstProvider))
                        return;
                }

                if (secondMethod == PaymentMethod.EWallet)
                {
                    secondProvider = AskWalletProvider();

                    if (string.IsNullOrWhiteSpace(secondProvider))
                        return;
                }

                var secondAmount = remaining - firstAmount;

                await _registerPaymentService.ExecuteSplitPaymentAsync(
                    _currentSaleId.Value,
                    firstAmount,
                    firstMethod.Value,
                    secondAmount,
                    secondMethod.Value,
                    firstProvider,
                    secondProvider);

                await FinishSaleAndShowInvoiceAsync();
            });
        }

        private static PaymentMethod? AskPaymentMethod(string title)
        {
            var answer = Interaction.InputBox(
                "اكتب إحدى القيم التالية بالإنجليزية:\n" +
                "Cash\nCard\nInstaPay\nEWallet",
                title,
                "Cash");

            if (Enum.TryParse(
                    answer,
                    true,
                    out PaymentMethod method))
            {
                return method;
            }

            MessageBox.Show(
                "وسيلة الدفع غير صحيحة.",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return null;
        }

        private static string? AskWalletProvider()
        {
            var provider = Interaction.InputBox(
                "أدخل اسم مزود المحفظة الإلكترونية:",
                "المحفظة الإلكترونية",
                "");

            return string.IsNullOrWhiteSpace(provider)
                ? null
                : provider.Trim();
        }

        // =========================================================
        // COMPLETE PAYMENT
        // =========================================================

        private async void CompletePayment_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue)
                    return;

                var details =
                    await _getSaleDetailsService.ExecuteAsync(
                        _currentSaleId.Value);

                if (details.Items.Count == 0)
                {
                    MessageBox.Show(
                        "أضف منتجاً واحداً على الأقل قبل إتمام البيع.",
                        "إتمام البيع",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                var remaining = details.RemainingAmount;

                if (remaining <= 0)
                {
                    await FinishSaleAndShowInvoiceAsync();
                    return;
                }

                string? walletProvider = null;

                if (_selectedPaymentMethod == PaymentMethod.EWallet)
                {
                    walletProvider = AskWalletProvider();

                    if (string.IsNullOrWhiteSpace(walletProvider))
                        return;
                }

                // The amount registered as payment must not exceed the
                // outstanding invoice balance. For cash, any excess
                // received is change, not an additional payment.
                var amountToRegister = remaining;

                await _registerPaymentService.ExecuteAsync(
                    _currentSaleId.Value,
                    amountToRegister,
                    _selectedPaymentMethod,
                    walletProvider);

                await FinishSaleAndShowInvoiceAsync();
            });
        }

        private async Task FinishSaleAndShowInvoiceAsync()
        {
            if (!_currentSaleId.HasValue)
                return;

            var completedSaleId = _currentSaleId.Value;

            // This service completes the sale and deducts stock in its
            // transaction. It may reject sales without valid user/stock data.
            await _completeSaleService.ExecuteAsync(completedSaleId);

            var details =
                await _getSaleDetailsService.ExecuteAsync(
                    completedSaleId);

            ShowInvoicePreview(details);

            await CreateNewSaleAsync();
        }

        // =========================================================
        // INVOICE PREVIEW AND PRINTING
        // =========================================================

        private void ShowInvoicePreview(dynamic details)
        {
            var document = new FlowDocument
            {
                PagePadding = new Thickness(35),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14
            };

            document.Blocks.Add(new Paragraph(
                new Run("UNIVERSAL POS"))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            });

            document.Blocks.Add(new Paragraph(
                new Run($"Invoice #{details.Id}"))
            {
                TextAlignment = TextAlignment.Center
            });

            document.Blocks.Add(new Paragraph(
                new Run($"Date: {details.SaleDate}")));

            var table = new Table();

            table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var header = new TableRow();
            header.Cells.Add(CreateCell("Product", true));
            header.Cells.Add(CreateCell("Qty", true));
            header.Cells.Add(CreateCell("Price", true));
            header.Cells.Add(CreateCell("Total", true));

            var headerGroup = new TableRowGroup();
            headerGroup.Rows.Add(header);
            table.RowGroups.Add(headerGroup);

            var rows = new TableRowGroup();

            foreach (var item in details.Items)
            {
                var row = new TableRow();

                row.Cells.Add(CreateCell(item.ProductName.ToString()));
                row.Cells.Add(CreateCell(item.Quantity.ToString()));
                row.Cells.Add(CreateCell(((decimal)item.UnitPrice).ToString("N2")));
                row.Cells.Add(CreateCell(((decimal)item.TotalPrice).ToString("N2")));

                rows.Rows.Add(row);
            }

            table.RowGroups.Add(rows);
            document.Blocks.Add(table);

            document.Blocks.Add(new Paragraph(
                new Run($"Total: {((decimal)details.TotalAmount):N2}"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            });

            var previewWindow = new Window
            {
                Title = $"Invoice #{details.Id}",
                Width = 750,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var panel = new DockPanel();

            var printButton = new Button
            {
                Content = "Print Invoice",
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            DockPanel.SetDock(printButton, Dock.Bottom);

            printButton.Click += (_, _) =>
            {
                var printDialog = new System.Windows.Controls.PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    document.PageWidth = printDialog.PrintableAreaWidth;
                    document.PageHeight = printDialog.PrintableAreaHeight;

                    printDialog.PrintDocument(
                        ((IDocumentPaginatorSource)document)
                            .DocumentPaginator,
                        $"Invoice #{details.Id}");
                }
            };

            panel.Children.Add(printButton);

            var viewer = new FlowDocumentScrollViewer
            {
                Document = document,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10)
            };

            panel.Children.Add(viewer);

            previewWindow.Content = panel;
            previewWindow.ShowDialog();
        }

        private static TableCell CreateCell(
            string text,
            bool isHeader = false)
        {
            return new TableCell(new Paragraph(new Run(text)))
            {
                Padding = new Thickness(5),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                FontWeight = isHeader
                    ? FontWeights.Bold
                    : FontWeights.Normal
            };
        }
        private async Task RunSafelyAsync(Func<Task> operation)
        {
            if (_isBusy)
                return;

            try
            {
                _isBusy = true;
                await operation();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ErrorMessageMapper.ToUserMessage(ex),
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
            }
        }
    }

    // =============================================================
    // VIEW MODELS USED BY THE XAML
    // =============================================================

    public class ProductCardViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string Barcode { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }

    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Total { get; set; }
        public string ImagePath { get; set; } = "";
    }
}