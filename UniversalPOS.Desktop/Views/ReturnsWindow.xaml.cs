using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.SalesReturns;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Desktop.Views;

public partial class ReturnsWindow : Window
{
    private readonly IServiceScope _scope;
    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly ISaleReturnRepository _returns;
    private readonly ProcessSaleReturnService _process;
    private readonly ObservableCollection<ReturnRow> _rows = new();
    private Sale? _sale;

    public ReturnsWindow()
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        _sales = _scope.ServiceProvider.GetRequiredService<ISaleRepository>();
        _products = _scope.ServiceProvider.GetRequiredService<IProductRepository>();
        _returns = _scope.ServiceProvider.GetRequiredService<ISaleReturnRepository>();
        _process = _scope.ServiceProvider.GetRequiredService<ProcessSaleReturnService>();
        ItemsGrid.ItemsSource = _rows;
        RefundMethodBox.ItemsSource = Enum.GetValues<PaymentMethod>();
        RefundMethodBox.SelectedItem = PaymentMethod.Cash;
        _rows.CollectionChanged += (_, _) => UpdateTotal();
        Closed += (_, _) => _scope.Dispose();
    }

    private async void SearchSale_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(SaleIdBox.Text.Trim(), out var saleId) || saleId <= 0)
        {
            MessageBox.Show("اكتب رقم فاتورة صحيح.", "بيانات غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            ItemsGrid.CommitEdit(); ItemsGrid.CommitEdit();
            _sale = await _sales.GetByIdAsync(saleId);
            _rows.Clear();
            if (_sale is null)
            {
                SaleSummaryText.Text = "لم يتم العثور على الفاتورة.";
                return;
            }
            if (_sale.Status != SaleReturnStatus.Completed)
            {
                _sale = null;
                SaleSummaryText.Text = "لا يمكن إرجاع فاتورة غير مكتملة.";
                MessageBox.Show("يمكن إرجاع الفواتير المكتملة والمدفوعة فقط.", "تعذر تحميل الفاتورة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var productIds = _sale.Items.Select(i => i.ProductId).Distinct().ToArray();
            var products = await _products.GetByIdsAsync(productIds);
            var names = products.ToDictionary(p => p.Id, p => p.Name);
            foreach (var item in _sale.Items)
            {
                var returned = await _returns.GetPreviouslyReturnedQuantityAsync(item.Id);
                var row = new ReturnRow(item.Id, item.ProductId, names.GetValueOrDefault(item.ProductId, $"صنف #{item.ProductId}"), item.UnitPrice, item.Quantity, returned);
                row.PropertyChanged += (_, _) => UpdateTotal();
                _rows.Add(row);
            }
            SaleSummaryText.Text = $"فاتورة #{_sale.Id}  |  التاريخ: {_sale.SaleDate:g}  |  الإجمالي: {_sale.TotalAmount:N2}  |  المدفوع: {_sale.PaidAmount:N2}";
            UpdateTotal();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "تعذر البحث عن الفاتورة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ProcessReturn_Click(object sender, RoutedEventArgs e)
    {
        ItemsGrid.CommitEdit(); ItemsGrid.CommitEdit();
        if (_sale is null)
        {
            MessageBox.Show("ابحث عن الفاتورة أولًا.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            MessageBox.Show("اكتب سبب الإرجاع.", "بيانات ناقصة", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var selected = _rows.Where(r => r.ReturnQuantity > 0).ToList();
        if (selected.Count == 0 || selected.Any(r => r.ReturnQuantity > r.RemainingQuantity))
        {
            MessageBox.Show("حدد كمية إرجاع أكبر من صفر ولا تتجاوز الكمية المتاحة لكل صنف.", "كميات غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (RefundMethodBox.SelectedItem is not PaymentMethod method) return;
        try
        {
            var requests = selected.Select(r => new SaleReturnItemRequest(r.SaleItemId, r.ReturnQuantity, r.IsRestockable)).ToArray();
            var id = await _process.ExecuteAsync(_sale.Id, ReasonBox.Text.Trim(), requests, method, string.IsNullOrWhiteSpace(ReferenceBox.Text) ? null : ReferenceBox.Text.Trim());
            MessageBox.Show($"تم تسجيل المرتجع بنجاح. رقم المرتجع: #{id}\nالقيمة: {selected.Sum(r => r.RefundAmount):N2}", "تم بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            SaleIdBox.Text = string.Empty; SaleSummaryText.Text = "أدخل رقم الفاتورة للبدء"; ReasonBox.Clear(); ReferenceBox.Clear(); _sale = null; _rows.Clear(); UpdateTotal();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "تعذر تسجيل المرتجع", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotal() => TotalText.Text = $"إجمالي المرتجع: {_rows.Sum(r => r.RefundAmount):N2}";

    private sealed class ReturnRow : INotifyPropertyChanged
    {
        public int SaleItemId { get; }
        public int ProductId { get; }
        public string ProductName { get; }
        public decimal UnitPrice { get; }
        public decimal SoldQuantity { get; }
        public decimal PreviouslyReturned { get; }
        public decimal RemainingQuantity => Math.Max(0, SoldQuantity - PreviouslyReturned);
        private decimal _returnQuantity;
        public decimal ReturnQuantity { get => _returnQuantity; set { _returnQuantity = value; OnChanged(); OnChanged(nameof(RefundAmount)); } }
        private bool _isRestockable = true;
        public bool IsRestockable { get => _isRestockable; set { _isRestockable = value; OnChanged(); } }
        public decimal RefundAmount => Math.Max(0, ReturnQuantity) * UnitPrice;
        public event PropertyChangedEventHandler? PropertyChanged;
        public ReturnRow(int saleItemId, int productId, string productName, decimal unitPrice, decimal soldQuantity, decimal previouslyReturned)
        { SaleItemId = saleItemId; ProductId = productId; ProductName = productName; UnitPrice = unitPrice; SoldQuantity = soldQuantity; PreviouslyReturned = previouslyReturned; }
        private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
