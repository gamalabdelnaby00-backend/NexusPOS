using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.DTOs.Sales;
using UniversalPOS.Application.Sales;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Views
{
    /// <summary>
    /// Cashier sales screen. Products stay hidden until the cashier asks to display them (button, search or category).
    /// Tax and discount are read from the sale itself: they are zero until a manager configures them elsewhere.
    /// </summary>
    public partial class SalesWindow : Window
    {
        private const int ProductsPageSize = 8;

        private sealed record CategoryOption(int Id, string Name);

        private static readonly SolidColorBrush GreenBrush = Frozen(Color.FromRgb(0x2B, 0x9A, 0x57));
        private static readonly SolidColorBrush RedBrush = Frozen(Color.FromRgb(0xD6, 0x45, 0x45));
        private static readonly SolidColorBrush BlueBrush = Frozen(Color.FromRgb(0x0A, 0x74, 0xF0));
        private static readonly SolidColorBrush InkBrush = Frozen(Color.FromRgb(0x12, 0x29, 0x4A));

        private readonly CreateSaleService _createSaleService;
        private readonly AddProductToSaleService _addProductToSaleService;
        private readonly GetProductsService _getProductsService;
        private readonly GetSaleDetailsService _getSaleDetailsService;
        private readonly RegisterPaymentService _registerPaymentService;
        private readonly CompleteSaleService _completeSaleService;
        private readonly CancelSaleService _cancelSaleService;
        private readonly SaleAdjustmentService _saleAdjustmentService;
        private readonly ISaleRepository _saleRepository;

        private readonly List<Product> _allProducts = new();
        private readonly List<CartItemViewModel> _cartItems = new();
        private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(20) };
        private Dictionary<int, decimal> _stock = new();
        private List<CategoryOption> _categories = new();

        private int? _currentSaleId;
        private int? _lastCompletedSaleId;
        private int? _selectedCategoryId;
        private int _page = 1;
        private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
        private bool _isBusy;
        private bool _ready;
        private bool _syncingCategory;
        private bool _showAllRequested;
        private bool _taxWarningShown;

        public SalesWindow()
        {
            InitializeComponent();
            FlowDirection = LocalizationManager.Current.FlowDirection;

            var services = App.Services;
            var currentUser = services.GetRequiredService<CurrentUserSession>();
            CashierNameText.Text = currentUser.DisplayName ?? currentUser.UserName ?? "Unknown user";
            CashierRoleText.Text = string.Join(" / ", currentUser.Roles);

            _createSaleService = services.GetRequiredService<CreateSaleService>();
            _addProductToSaleService = services.GetRequiredService<AddProductToSaleService>();
            _getProductsService = services.GetRequiredService<GetProductsService>();
            _getSaleDetailsService = services.GetRequiredService<GetSaleDetailsService>();
            _registerPaymentService = services.GetRequiredService<RegisterPaymentService>();
            _completeSaleService = services.GetRequiredService<CompleteSaleService>();
            _cancelSaleService = services.GetRequiredService<CancelSaleService>();
            _saleAdjustmentService = services.GetRequiredService<SaleAdjustmentService>();
            _saleRepository = services.GetRequiredService<ISaleRepository>();

            UpdateClock();
            _clock.Tick += (_, _) => UpdateClock();
            _clock.Start();
            Closed += (_, _) => _clock.Stop();

            SelectPaymentMethod(PaymentMethod.Cash);
            _ready = true;
            RefreshProductView();

            Loaded += SalesWindow_Loaded;
        }

        private static string T(string arabic, string english) => SalesText.T(arabic, english);

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private void UpdateClock() =>
            DateText.Text = DateTime.Now.ToString("dddd d MMMM yyyy  h:mm tt", CultureInfo.CurrentUICulture);

        // =========================================================
        // INITIALIZATION
        // =========================================================

        private async void SalesWindow_Loaded(object sender, RoutedEventArgs e)
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

        /// <summary>Loads active products, their available quantities and the categories. Nothing is displayed yet.</summary>
        private async Task LoadProductsAsync()
        {
            var products = await _getProductsService.ExecuteAsync();
            _allProducts.Clear();
            _allProducts.AddRange(products.Where(p => p.IsActive));

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                _stock = await db.StockBalances.AsNoTracking().ToDictionaryAsync(b => b.ProductId, b => b.Quantity);
                _categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name)
                    .Select(c => new CategoryOption(c.Id, c.Name)).ToListAsync();
            }

            BuildCategoryControls();
            RefreshProductView();
        }

        private async Task CreateNewSaleAsync()
        {
            _currentSaleId = await _createSaleService.ExecuteAsync();
            ReceiptNumberText.Text = T($"فاتورة رقم {_currentSaleId}", $"Invoice #{_currentSaleId}");
            _cartItems.Clear();
            await RefreshCartAsync();
        }

        private decimal StockOf(int productId) => _stock.TryGetValue(productId, out var quantity) ? quantity : 0m;

        // =========================================================
        // CATEGORIES AND FILTERS
        // =========================================================

        private void BuildCategoryControls()
        {
            _syncingCategory = true;
            SectionComboBox.Items.Clear();
            SectionComboBox.Items.Add(T("جميع الأقسام", "All categories"));
            foreach (var category in _categories) SectionComboBox.Items.Add(category.Name);
            SectionComboBox.SelectedIndex = SelectedCategoryIndex();
            _syncingCategory = false;

            CategoriesPanel.Children.Clear();
            CategoriesPanel.Children.Add(CreateCategoryButton(null, T("كل الأقسام", "All categories")));
            foreach (var category in _categories)
                CategoriesPanel.Children.Add(CreateCategoryButton(category.Id, category.Name));
            UpdateCategoryHighlight();
        }

        private int SelectedCategoryIndex()
        {
            if (_selectedCategoryId is not int id) return 0;
            int index = _categories.FindIndex(c => c.Id == id);
            return index < 0 ? 0 : index + 1;
        }

        private Button CreateCategoryButton(int? categoryId, string text)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            content.Children.Add(new System.Windows.Shapes.Path
            {
                Data = (Geometry)FindResource("SIcon.Category"), Fill = BlueBrush, Width = 18, Height = 18, Stretch = Stretch.Uniform
            });
            content.Children.Add(new TextBlock { Text = text, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });

            var button = new Button
            {
                Style = (Style)FindResource("RoundButton"), Height = 46, Margin = new Thickness(0, 0, 0, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = InkBrush, Tag = categoryId, Content = content
            };
            button.Click += (_, _) => SelectCategory(categoryId);
            return button;
        }

        private void UpdateCategoryHighlight()
        {
            foreach (var child in CategoriesPanel.Children.OfType<Button>())
            {
                bool selected = (child.Tag as int?) == _selectedCategoryId;
                child.Background = selected ? Frozen(Color.FromRgb(0xDC, 0xEB, 0xFC)) : Brushes.White;
                child.BorderBrush = selected ? BlueBrush : Frozen(Color.FromRgb(0xD6, 0xE0, 0xEC));
            }
        }

        private void SelectCategory(int? categoryId)
        {
            _selectedCategoryId = categoryId;
            if (categoryId is null) _showAllRequested = true;   // choosing "all sections" is an explicit request to display products

            _syncingCategory = true;
            SectionComboBox.SelectedIndex = SelectedCategoryIndex();
            _syncingCategory = false;

            _page = 1;
            UpdateCategoryHighlight();
            RefreshProductView();
        }

        private void SectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || _syncingCategory || SectionComboBox.SelectedIndex < 0) return;
            int index = SectionComboBox.SelectedIndex;
            SelectCategory(index <= 0 ? null : _categories[index - 1].Id);
        }

        private void AvailabilityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            _page = 1;
            RefreshProductView();
        }

        // =========================================================
        // PRODUCT DISPLAY (only on request) AND SEARCH
        // =========================================================

        private bool ProductsAreVisible =>
            _showAllRequested
            || !string.IsNullOrWhiteSpace(SearchBox.Text)
            || _selectedCategoryId.HasValue
            || AvailabilityComboBox.SelectedIndex > 0;

        private IEnumerable<Product> FilterProducts()
        {
            string query = SearchBox.Text?.Trim() ?? "";
            IEnumerable<Product> result = _allProducts;

            if (_selectedCategoryId is int categoryId)
                result = result.Where(p => p.CategoryId == categoryId);

            int availability = AvailabilityComboBox.SelectedIndex;
            if (availability == 1) result = result.Where(p => StockOf(p.Id) > 0m);
            else if (availability == 2) result = result.Where(p => StockOf(p.Id) <= 0m);

            if (query.Length > 0)
            {
                result = result.Where(p =>
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return result.OrderBy(p => p.Name);
        }

        private void RefreshProductView()
        {
            if (!_ready) return;

            bool visible = ProductsAreVisible;
            ShowProductsButton.Content = visible ? T("إخفاء المنتجات", "Hide products") : T("عرض المنتجات", "Show products");
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

            if (!visible)
            {
                ProductsItems.ItemsSource = null;
                ProductsScroll.Visibility = Visibility.Collapsed;
                ProductsEmptyState.Visibility = Visibility.Visible;
                ProductsEmptyText.Text = SalesText.ProductsHidden;
                ProductsCountText.Text = "";
                PagerPanel.Children.Clear();
                return;
            }

            var filtered = FilterProducts().ToList();
            int pages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)ProductsPageSize));
            _page = Math.Min(Math.Max(1, _page), pages);
            ProductsCountText.Text = $"({filtered.Count})";

            if (filtered.Count == 0)
            {
                ProductsItems.ItemsSource = null;
                ProductsScroll.Visibility = Visibility.Collapsed;
                ProductsEmptyState.Visibility = Visibility.Visible;
                ProductsEmptyText.Text = T("لا توجد منتجات مطابقة.", "No matching products.");
                PagerPanel.Children.Clear();
                return;
            }

            // Only the products of the current page are turned into cards (pictures load lazily, 8 at a time).
            var cards = filtered.Skip((_page - 1) * ProductsPageSize).Take(ProductsPageSize).Select(ToCard).ToList();
            ProductsItems.ItemsSource = cards;
            ProductsEmptyState.Visibility = Visibility.Collapsed;
            ProductsScroll.Visibility = Visibility.Visible;
            BuildPager(pages);
        }

        private ProductCardViewModel ToCard(Product product)
        {
            decimal quantity = StockOf(product.Id);
            bool inStock = quantity > 0m;
            return new ProductCardViewModel
            {
                ProductId = product.Id,
                Name = product.Name,
                Code = product.Code,
                Barcode = product.Barcode,
                Price = product.Price,
                Quantity = quantity,
                InStock = inStock,
                StockText = inStock ? T($"متاح: {quantity:0.##}", $"In stock: {quantity:0.##}") : T("غير متوفر", "Out of stock"),
                StockBrush = inStock ? GreenBrush : RedBrush,
                InStockVisibility = inStock ? Visibility.Visible : Visibility.Collapsed,
                ImageSource = LoadProductImage(product)
            };
        }

        private static ImageSource? LoadProductImage(Product product)
        {
            try
            {
                string? path = null;
                if (!string.IsNullOrWhiteSpace(product.ImageKey))
                {
                    var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS", "ProductImages");
                    var full = Path.GetFullPath(Path.Combine(root, product.ImageKey.Replace('/', Path.DirectorySeparatorChar)));
                    if (full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                        path = full;
                }
                if (path is null)
                {
                    var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Products", $"{product.Id}.png");
                    if (File.Exists(bundled)) path = bundled;
                }
                if (path is null) return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 220;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void BuildPager(int pages)
        {
            PagerPanel.Children.Clear();
            if (pages <= 1) return;

            PagerPanel.Children.Add(PagerButton("«", _page > 1, false, () => GoToPage(1)));
            PagerPanel.Children.Add(PagerButton("‹", _page > 1, false, () => GoToPage(_page - 1)));
            int start = Math.Max(1, Math.Min(_page - 2, pages - 4));
            int end = Math.Min(pages, start + 4);
            for (int number = start; number <= end; number++)
            {
                int target = number;
                PagerPanel.Children.Add(PagerButton(number.ToString(), true, number == _page, () => GoToPage(target)));
            }
            PagerPanel.Children.Add(PagerButton("›", _page < pages, false, () => GoToPage(_page + 1)));
            PagerPanel.Children.Add(PagerButton("»", _page < pages, false, () => GoToPage(pages)));
        }

        private Button PagerButton(string text, bool enabled, bool current, Action onClick)
        {
            var button = new Button
            {
                Style = (Style)FindResource("PagerButton"), Content = text, IsEnabled = enabled,
                Background = current ? BlueBrush : Brushes.White,
                Foreground = current ? Brushes.White : InkBrush,
                BorderBrush = current ? BlueBrush : Frozen(Color.FromRgb(0xD2, 0xE2, 0xF5))
            };
            button.Click += (_, _) => onClick();
            return button;
        }

        private void GoToPage(int page)
        {
            _page = page;
            RefreshProductView();
        }

        private void ShowProducts_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsAreVisible)
            {
                // Hide again: clear the search, the category and the availability filter.
                _showAllRequested = false;
                _selectedCategoryId = null;
                SearchBox.Text = "";
                _syncingCategory = true;
                SectionComboBox.SelectedIndex = 0;
                AvailabilityComboBox.SelectedIndex = 0;
                _syncingCategory = false;
                UpdateCategoryHighlight();
            }
            else
            {
                _showAllRequested = true;
            }

            _page = 1;
            RefreshProductView();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            _page = 1;
            RefreshProductView();
        }

        /// <summary>Enter adds the exact code / barcode match (barcode scanners send Enter), or the only search result.</summary>
        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            string query = SearchBox.Text?.Trim() ?? "";
            if (query.Length == 0) return;

            var match = FindProduct(query);
            if (match is null)
            {
                var results = FilterProducts().Take(2).ToList();
                if (results.Count == 1) match = results[0];
            }

            if (match is null)
            {
                MessageBox.Show(T("لا يوجد منتج بهذا الكود أو الباركود.", "No product matches this code or barcode."), T("البحث", "Search"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SearchBox.Clear();
            await AddProductToCartAsync(match);
        }

        private Product? FindProduct(string codeOrBarcode) =>
            _allProducts.FirstOrDefault(p =>
                string.Equals(p.Barcode, codeOrBarcode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Code, codeOrBarcode, StringComparison.OrdinalIgnoreCase));

        private void ScanBarcode_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        private async void AddManualProduct_Click(object sender, RoutedEventArgs e)
        {
            var input = Interaction.InputBox(T("أدخل كود المنتج أو الباركود:", "Enter the product code or barcode:"), T("إضافة منتج يدوي", "Add product manually"), "");
            if (string.IsNullOrWhiteSpace(input)) return;

            var match = FindProduct(input.Trim());
            if (match is null)
            {
                MessageBox.Show(T("لا يوجد منتج بهذا الكود أو الباركود.", "No product matches this code or barcode."), T("إضافة منتج يدوي", "Add product manually"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await AddProductToCartAsync(match);
        }

        // =========================================================
        // ADD PRODUCT TO CART
        // =========================================================

        private async void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ProductCardViewModel card) return;

            var product = _allProducts.FirstOrDefault(p => p.Id == card.ProductId);
            if (product is not null) await AddProductToCartAsync(product);
        }

        private async Task AddProductToCartAsync(Product product)
        {
            decimal available = StockOf(product.Id);
            decimal inCart = _cartItems.Where(i => i.ProductId == product.Id).Sum(i => i.Quantity);

            if (available <= 0m)
            {
                MessageBox.Show(T($"المنتج «{product.Name}» غير متوفر في المخزون.", $"Product '{product.Name}' is out of stock."), T("المخزون", "Stock"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (inCart + 1m > available)
            {
                MessageBox.Show(T($"الكمية المتاحة من «{product.Name}» هي {available:0.##} فقط، وقد أضفتها كلها إلى الفاتورة.", $"Only {available:0.##} of '{product.Name}' are available, and all of them are already on the invoice."),
                    T("المخزون", "Stock"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue) await CreateNewSaleAsync();

                await _addProductToSaleService.ExecuteAsync(_currentSaleId!.Value, product.Barcode, 1m);
                await RefreshCartAsync();
            });
        }

        // =========================================================
        // CART REFRESH AND TOTALS
        // =========================================================

        private async Task RefreshCartAsync()
        {
            if (!_currentSaleId.HasValue) return;
            int saleId = _currentSaleId.Value;

            await ApplyConfiguredTaxAsync(saleId);

            var details = await _getSaleDetailsService.ExecuteAsync(saleId);
            var sale = await _saleRepository.GetByIdAsync(saleId);

            _cartItems.Clear();
            int index = 1;
            foreach (var item in details.Items)
            {
                _cartItems.Add(new CartItemViewModel
                {
                    Index = index++,
                    ProductId = item.ProductId,
                    Name = item.ProductName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    Total = item.TotalPrice
                });
            }

            CartItems.ItemsSource = null;
            CartItems.ItemsSource = _cartItems.ToList();
            EmptyCartText.Visibility = _cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            UpdateTotals(sale, details.TotalAmount);
        }

        /// <summary>
        /// Copies the configured tax onto the draft sale. With tax disabled (the default) this clears it, so the
        /// invoice stays at zero tax until a manager turns it on from the settings screen.
        /// </summary>
        private async Task ApplyConfiguredTaxAsync(int saleId)
        {
            try
            {
                await _saleAdjustmentService.ApplyConfiguredTaxAsync(saleId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Could not apply the configured tax: " + ex);
                if (_taxWarningShown) return;
                _taxWarningShown = true;
                MessageBox.Show(T("تعذر تطبيق إعداد الضريبة على الفاتورة، وستظهر الضريبة صفرًا.", "Could not apply the tax setting to the invoice; tax will show as zero."), T("الضريبة", "Tax"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateTotals(Sale? sale, decimal fallbackTotal)
        {
            decimal subtotal = sale?.Subtotal ?? fallbackTotal;
            decimal discount = sale?.DiscountAmount ?? 0m;
            decimal tax = sale?.TaxAmount ?? 0m;
            decimal total = sale?.TotalAmount ?? fallbackTotal;

            SubtotalText.Text = subtotal.ToString("N2");
            DiscountText.Text = discount.ToString("N2");
            DiscountPercentText.Text = (subtotal > 0m ? discount / subtotal * 100m : 0m).ToString("0.##") + "%";

            if (sale?.TaxRate is decimal rate)
            {
                string name = string.IsNullOrWhiteSpace(sale.TaxName) ? T("الضريبة", "Tax") : sale.TaxName;
                VatLabelText.Text = $"{name} ({rate:0.##}%){(sale.TaxIncluded ? T(" - شامل", " - inclusive") : "")}:";
            }
            else
            {
                VatLabelText.Text = T("الضريبة (0%):", "Tax (0%):");
            }

            VatText.Text = tax.ToString("N2");
            TotalText.Text = total.ToString("N2");
        }

        // =========================================================
        // INCREASE / DECREASE QUANTITY
        // =========================================================

        private async void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not CartItemViewModel item) return;

            decimal available = StockOf(item.ProductId);
            if (item.Quantity + 1m > available)
            {
                MessageBox.Show(T($"الكمية المتاحة من «{item.Name}» هي {available:0.##} فقط.", $"Only {available:0.##} of '{item.Name}' are available."), T("المخزون", "Stock"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ChangeQuantityAsync(item, item.Quantity + 1m);
        }

        private async void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not CartItemViewModel item) return;

            if (item.Quantity <= 1m)
            {
                MessageBox.Show(T("استخدم زر الحذف لإزالة المنتج من الفاتورة.", "Use the delete button to remove the product from the invoice."), T("تعديل الكمية", "Edit quantity"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ChangeQuantityAsync(item, item.Quantity - 1m);
        }

        private async Task ChangeQuantityAsync(CartItemViewModel item, decimal newQuantity)
        {
            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue) return;

                var sale = await _saleRepository.GetByIdAsync(_currentSaleId.Value)
                    ?? throw new InvalidOperationException(T("الفاتورة غير موجودة.", "The invoice was not found."));
                var saleItem = sale.Items.FirstOrDefault(x => x.ProductId == item.ProductId)
                    ?? throw new InvalidOperationException(T("المنتج غير موجود في الفاتورة.", "The product is not on the invoice."));

                sale.ChangeItemQuantity(saleItem, newQuantity);
                await _saleRepository.UpdateAsync(sale);
                await RefreshCartAsync();
            });
        }

        // =========================================================
        // REMOVE PRODUCT / CLEAR CART / CANCEL SALE
        // =========================================================

        private async void RemoveCartItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not CartItemViewModel item) return;

            var answer = MessageBox.Show(T($"هل تريد حذف {item.Name} من الفاتورة؟", $"Remove {item.Name} from the invoice?"), T("تأكيد الحذف", "Confirm removal"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue) return;

                var sale = await _saleRepository.GetByIdAsync(_currentSaleId.Value)
                    ?? throw new InvalidOperationException(T("الفاتورة غير موجودة.", "The invoice was not found."));
                var saleItem = sale.Items.FirstOrDefault(x => x.ProductId == item.ProductId);
                if (saleItem is null) return;

                sale.RemoveItem(saleItem);
                await _saleRepository.UpdateAsync(sale);
                await RefreshCartAsync();
            });
        }

        private async void ClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0) return;

            var answer = MessageBox.Show(T("هل تريد حذف كل المنتجات من الفاتورة؟", "Remove all products from the invoice?"), T("تفريغ السلة", "Clear cart"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue) return;

                var sale = await _saleRepository.GetByIdAsync(_currentSaleId.Value)
                    ?? throw new InvalidOperationException(T("الفاتورة غير موجودة.", "The invoice was not found."));
                foreach (var saleItem in sale.Items.ToList()) sale.RemoveItem(saleItem);

                await _saleRepository.UpdateAsync(sale);
                await RefreshCartAsync();
            });
        }

        private async void CancelSale_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(T("هل تريد إلغاء هذه الفاتورة والبدء بفاتورة جديدة؟", "Cancel this invoice and start a new one?"), T("إلغاء الفاتورة", "Cancel invoice"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            await RunSafelyAsync(async () =>
            {
                if (_currentSaleId.HasValue) await _cancelSaleService.ExecuteAsync(_currentSaleId.Value);
                await CreateNewSaleAsync();
            });
        }

        // =========================================================
        // PAYMENT METHOD SELECTION
        // =========================================================

        private void PaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            if (!Enum.TryParse(button.Tag?.ToString(), true, out PaymentMethod method))
            {
                MessageBox.Show(T("وسيلة الدفع غير معروفة.", "Unknown payment method."), T("الدفع", "Payment"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectPaymentMethod(method);
        }

        private void SelectPaymentMethod(PaymentMethod method)
        {
            _selectedPaymentMethod = method;
            StylePaymentButton(CashButton, CashCheck, method == PaymentMethod.Cash);
            StylePaymentButton(CardButton, CardCheck, method == PaymentMethod.Card);
            StylePaymentButton(InstaPayButton, InstaPayCheck, method == PaymentMethod.InstaPay);
            StylePaymentButton(EWalletButton, EWalletCheck, method == PaymentMethod.EWallet);
        }

        private static void StylePaymentButton(Button button, Border check, bool selected)
        {
            button.Background = selected ? Frozen(Color.FromRgb(0xEA, 0xF4, 0xFF)) : Brushes.White;
            button.BorderBrush = selected ? BlueBrush : Frozen(Color.FromRgb(0xD6, 0xE0, 0xEC));
            button.BorderThickness = new Thickness(selected ? 2 : 1);
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }

        // =========================================================
        // SPLIT PAYMENT
        // =========================================================

        private async void SplitPayment_Click(object sender, RoutedEventArgs e)
        {
            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue) return;

                var details = await _getSaleDetailsService.ExecuteAsync(_currentSaleId.Value);
                if (details.Items.Count == 0)
                {
                    MessageBox.Show(T("أضف منتجات إلى الفاتورة أولاً.", "Add products to the invoice first."), T("الدفع المقسّم", "Split payment"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var remaining = details.RemainingAmount;

                var amountText = Interaction.InputBox(
                    T($"المتبقي: {remaining:N2}\n", $"Remaining: {remaining:N2}\n") + T("أدخل المبلغ الذي ستدفعه بوسيلة الدفع الأولى:", "Enter the amount to pay with the first payment method:"),
                    T("الدفع المقسّم", "Split payment"),
                    (remaining / 2m).ToString("0.00", CultureInfo.InvariantCulture));

                if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var firstAmount) &&
                    !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.CurrentCulture, out firstAmount))
                {
                    return;
                }

                if (firstAmount <= 0 || firstAmount >= remaining)
                {
                    MessageBox.Show(T("المبلغ الأول يجب أن يكون أكبر من صفر وأقل من المتبقي.", "The first amount must be greater than zero and less than the remaining balance."), T("الدفع المقسّم", "Split payment"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var firstMethod = AskPaymentMethod(T("اختر وسيلة الدفع الأولى", "Choose the first payment method"));
                if (!firstMethod.HasValue) return;

                var secondMethod = AskPaymentMethod(T("اختر وسيلة الدفع الثانية", "Choose the second payment method"));
                if (!secondMethod.HasValue) return;

                if (firstMethod.Value == secondMethod.Value)
                {
                    MessageBox.Show(T("يجب اختيار وسيلتي دفع مختلفتين.", "Choose two different payment methods."), T("الدفع المقسّم", "Split payment"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string? firstProvider = null;
                string? secondProvider = null;

                if (firstMethod == PaymentMethod.EWallet)
                {
                    firstProvider = AskWalletProvider();
                    if (string.IsNullOrWhiteSpace(firstProvider)) return;
                }

                if (secondMethod == PaymentMethod.EWallet)
                {
                    secondProvider = AskWalletProvider();
                    if (string.IsNullOrWhiteSpace(secondProvider)) return;
                }

                var secondAmount = remaining - firstAmount;

                await _registerPaymentService.ExecuteSplitPaymentAsync(
                    _currentSaleId.Value, firstAmount, firstMethod.Value,
                    secondAmount, secondMethod.Value, firstProvider, secondProvider);

                await FinishSaleAndShowInvoiceAsync();
            });
        }

        private static PaymentMethod? AskPaymentMethod(string title)
        {
            var answer = Interaction.InputBox(
                T("اكتب إحدى القيم التالية بالإنجليزية:\n", "Type one of the following values:\n") + "Cash\nCard\nInstaPay\nEWallet", title, "Cash");

            if (Enum.TryParse(answer, true, out PaymentMethod method)) return method;

            MessageBox.Show(T("وسيلة الدفع غير صحيحة.", "Invalid payment method."), title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        private static string? AskWalletProvider()
        {
            var provider = Interaction.InputBox(T("أدخل اسم مزود المحفظة الإلكترونية:", "Enter the e-wallet provider name:"), T("المحفظة الإلكترونية", "E-wallet"), "");
            return string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();
        }

        // =========================================================
        // COMPLETE PAYMENT
        // =========================================================

        private async void CompletePayment_Click(object sender, RoutedEventArgs e)
        {
            await RunSafelyAsync(async () =>
            {
                if (!_currentSaleId.HasValue) return;

                var details = await _getSaleDetailsService.ExecuteAsync(_currentSaleId.Value);
                if (details.Items.Count == 0)
                {
                    MessageBox.Show(T("أضف منتجاً واحداً على الأقل قبل إتمام البيع.", "Add at least one product before completing the sale."), T("إتمام البيع", "Complete sale"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    if (string.IsNullOrWhiteSpace(walletProvider)) return;
                }

                // The registered payment never exceeds the outstanding balance of the invoice.
                await _registerPaymentService.ExecuteAsync(_currentSaleId.Value, remaining, _selectedPaymentMethod, walletProvider);

                await FinishSaleAndShowInvoiceAsync();
            });
        }

        private async Task FinishSaleAndShowInvoiceAsync()
        {
            if (!_currentSaleId.HasValue) return;
            var completedSaleId = _currentSaleId.Value;

            // Completes the sale and deducts stock in one transaction.
            await _completeSaleService.ExecuteAsync(completedSaleId);
            _lastCompletedSaleId = completedSaleId;

            var details = await _getSaleDetailsService.ExecuteAsync(completedSaleId);
            ShowInvoicePreview(details);

            await LoadProductsAsync();      // quantities changed
            await CreateNewSaleAsync();
        }

        // =========================================================
        // INVOICE PREVIEW AND PRINTING
        // =========================================================

        private async void PrintInvoice_Click(object sender, RoutedEventArgs e)
        {
            await RunSafelyAsync(async () =>
            {
                int? saleId = _cartItems.Count > 0 ? _currentSaleId : _lastCompletedSaleId;
                if (!saleId.HasValue)
                {
                    MessageBox.Show(T("لا توجد فاتورة للطباعة. أضف منتجات أو أتمم عملية بيع أولاً.", "There is no invoice to print. Add products or complete a sale first."), T("طباعة الفاتورة", "Print invoice"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var details = await _getSaleDetailsService.ExecuteAsync(saleId.Value);
                ShowInvoicePreview(details);
            });
        }

        private void ShowInvoicePreview(SaleDetailsDto details)
        {
            var document = new FlowDocument
            {
                PagePadding = new Thickness(35),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14
            };

            document.Blocks.Add(new Paragraph(new Run("UNIVERSAL POS"))
            {
                FontSize = 24, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center
            });
            document.Blocks.Add(new Paragraph(new Run($"Invoice #{details.Id}")) { TextAlignment = TextAlignment.Center });
            document.Blocks.Add(new Paragraph(new Run($"Date: {details.SaleDate}")));

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
                row.Cells.Add(CreateCell(item.ProductName));
                row.Cells.Add(CreateCell(item.Quantity.ToString("0.###")));
                row.Cells.Add(CreateCell(item.UnitPrice.ToString("N2")));
                row.Cells.Add(CreateCell(item.TotalPrice.ToString("N2")));
                rows.Rows.Add(row);
            }
            table.RowGroups.Add(rows);
            document.Blocks.Add(table);

            document.Blocks.Add(new Paragraph(new Run($"Total: {details.TotalAmount:N2}"))
            {
                FontSize = 18, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            });

            var previewWindow = new Window
            {
                Title = $"Invoice #{details.Id}", Width = 750, Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this
            };

            var panel = new DockPanel();
            var printButton = new Button
            {
                Content = "Print Invoice", Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(printButton, Dock.Bottom);

            printButton.Click += (_, _) =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    document.PageWidth = printDialog.PrintableAreaWidth;
                    document.PageHeight = printDialog.PrintableAreaHeight;
                    printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Invoice #{details.Id}");
                }
            };

            panel.Children.Add(printButton);
            panel.Children.Add(new FlowDocumentScrollViewer
            {
                Document = document, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10)
            });

            previewWindow.Content = panel;
            previewWindow.ShowDialog();
        }

        private static TableCell CreateCell(string text, bool isHeader = false) =>
            new(new Paragraph(new Run(text)))
            {
                Padding = new Thickness(5),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal
            };

        // =========================================================
        // SAFE EXECUTION
        // =========================================================

        private async Task RunSafelyAsync(Func<Task> operation)
        {
            if (_isBusy) return;

            try
            {
                _isBusy = true;
                await operation();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), T("خطأ", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
        public string Code { get; set; } = "";
        public string Barcode { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public bool InStock { get; set; }
        public string StockText { get; set; } = "";
        public Brush StockBrush { get; set; } = Brushes.Gray;
        public Visibility InStockVisibility { get; set; } = Visibility.Collapsed;
        public ImageSource? ImageSource { get; set; }
        public Visibility PlaceholderVisibility => ImageSource is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public class CartItemViewModel
    {
        public int Index { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Total { get; set; }
        public string QuantityText => Quantity.ToString("0.###");
    }
}

namespace UniversalPOS.Desktop.Views
{
    /// <summary>
    /// Texts of the sales screen in both languages. The XAML reads these through x:Static when the window opens,
    /// so the screen always matches the language chosen on the sign-in screen or inside the application.
    /// </summary>
    public static class SalesText
    {
        public static bool IsArabic => AppearanceManager.Current.Language.Equals("ar", StringComparison.OrdinalIgnoreCase);
        public static string T(string arabic, string english) => IsArabic ? arabic : english;

        public static string WindowTitle => T("المبيعات | NexusPOS", "Sales | NexusPOS");
        public static string Increase => T("زيادة الكمية", "Increase quantity");
        public static string Decrease => T("إنقاص الكمية", "Decrease quantity");
        public static string RemoveFromInvoice => T("حذف من الفاتورة", "Remove from invoice");
        public static string SalesTitle => T("مبيعات", "Sales");
        public static string AddManual => T("إضافة منتج يدوي", "Add product manually");
        public static string ScanBarcode => T("مسح باركود", "Scan barcode");
        public static string AllStatuses => T("جميع الحالات", "All statuses");
        public static string Available => T("متوفر", "Available");
        public static string OutOfStockShort => T("غير متوفر", "Out of stock");
        public static string SearchHint => T("ابحث بالكود أو الباركود أو اسم المنتج...", "Search by code, barcode or product name...");
        public static string InvoiceItems => T("العناصر المباعة", "Invoice items");
        public static string ColProduct => T("المنتج", "Product");
        public static string ColQuantity => T("الكمية", "Qty");
        public static string ColUnitPrice => T("سعر الوحدة", "Unit price");
        public static string ColTotal => T("الإجمالي", "Total");
        public static string EmptyCart => T("لم تتم إضافة أي منتج بعد.", "No products added yet.");
        public static string Subtotal => T("المجموع الفرعي:", "Subtotal:");
        public static string Discount => T("الخصم:", "Discount:");
        public static string Tax => T("الضريبة:", "Tax:");
        public static string GrandTotal => T("الإجمالي الكلي:", "Grand total:");
        public static string Products => T("المنتجات", "Products");
        public static string ProductsHidden => T("المنتجات غير معروضة. اضغط «عرض المنتجات» أو ابحث بالكود أو اسم المنتج أو اختر قسمًا.", "Products are hidden. Press “Show products”, search by code or name, or choose a category.");
        public static string Categories => T("الأقسام", "Categories");
        public static string CompleteSale => T("إتمام البيع", "Complete sale");
        public static string PaymentMethodLabel => T("طريقة الدفع", "Payment method");
        public static string Cash => T("نقدًا", "Cash");
        public static string Card => T("بطاقة بنكية", "Bank card");
        public static string InstaPay => T("محفظة Instapay", "Instapay wallet");
        public static string EWallet => T("محفظة إلكترونية", "E-wallet");
        public static string ClearCart => T("تفريغ السلة", "Clear cart");
        public static string CancelButton => T("إلغاء", "Cancel");
        public static string SplitPayment => T("دفع مقسّم", "Split payment");
        public static string PrintInvoice => T("طباعة الفاتورة", "Print invoice");
    }
}
