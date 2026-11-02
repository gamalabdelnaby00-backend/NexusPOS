using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NexusPOS
{
    public partial class PurchaseEditorWindow : Window
    {
        private readonly ObservableCollection<PurchaseItemRow> _purchaseItems = new();

        public PurchaseEditorWindow()
        {
            InitializeComponent();

            PurchaseItemsDataGrid.ItemsSource = _purchaseItems;

            InvoiceDatePicker.SelectedDate = DateTime.Today;

            UpdateTotal();
        }

        private void SupplierComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            // يمكن هنا تحميل بيانات المورد المختار
            // بعد ربط الشاشة بكيان الموردين في قاعدة البيانات.
        }

        private void AddItemButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ProductComboBox.SelectedItem == null)
            {
                MessageBox.Show(
                    "من فضلك اختر المنتج أولًا.",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ProductComboBox.Focus();
                return;
            }

            if (!int.TryParse(QuantityTextBox.Text, out int quantity)
                || quantity <= 0)
            {
                MessageBox.Show(
                    "أدخل كمية صحيحة أكبر من صفر.",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                QuantityTextBox.Focus();
                return;
            }

            if (!decimal.TryParse(
                    UnitCostTextBox.Text,
                    out decimal unitCost)
                || unitCost < 0)
            {
                MessageBox.Show(
                    "أدخل تكلفة صحيحة للمنتج.",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                UnitCostTextBox.Focus();
                return;
            }

            string productName =
                ProductComboBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(productName))
            {
                MessageBox.Show(
                    "اسم المنتج غير صالح.",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var existingItem = _purchaseItems.FirstOrDefault(
                item => item.ProductName == productName
                     && item.UnitCost == unitCost);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;

                int index = _purchaseItems.IndexOf(existingItem);

                _purchaseItems[index] = existingItem;
            }
            else
            {
                _purchaseItems.Add(new PurchaseItemRow
                {
                    ProductName = productName,
                    Quantity = quantity,
                    UnitCost = unitCost
                });
            }

            PurchaseItemsDataGrid.Items.Refresh();

            UpdateTotal();

            QuantityTextBox.Clear();
            UnitCostTextBox.Clear();
            ProductComboBox.SelectedIndex = -1;

            ProductComboBox.Focus();
        }

        private void RemoveItemButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not PurchaseItemRow item)
                return;

            _purchaseItems.Remove(item);

            UpdateTotal();
        }

        private void UpdateTotal()
        {
            decimal total = _purchaseItems.Sum(
                item => item.Quantity * item.UnitCost);

            TotalTextBlock.Text = total.ToString("N2");
        }

        private bool ValidatePurchase()
        {
            if (SupplierComboBox.SelectedItem == null)
            {
                MessageBox.Show(
                    "من فضلك اختر المورد.",
                    "بيانات ناقصة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SupplierComboBox.Focus();
                return false;
            }

            if (InvoiceDatePicker.SelectedDate == null)
            {
                MessageBox.Show(
                    "من فضلك اختر تاريخ الفاتورة.",
                    "بيانات ناقصة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                InvoiceDatePicker.Focus();
                return false;
            }

            if (_purchaseItems.Count == 0)
            {
                MessageBox.Show(
                    "أضف صنفًا واحدًا على الأقل إلى الفاتورة.",
                    "بيانات ناقصة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ProductComboBox.Focus();
                return false;
            }

            return true;
        }

        private void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ValidatePurchase())
                return;

            MessageBox.Show(
                "تم التحقق من بيانات الفاتورة.\n" +
                "لم يتم حفظها في قاعدة البيانات بعد؛ " +
                "يلزم ربطها بكيانات المشتريات الموجودة في المشروع.",
                "حفظ الفاتورة",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ReceiveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ValidatePurchase())
                return;

            MessageBox.Show(
                "تم التحقق من بيانات الفاتورة.\n" +
                "لم يتم تسجيل الاستلام أو تحديث المخزون؛ " +
                "يجب تنفيذ ذلك داخل عملية قاعدة بيانات واحدة.",
                "استلام المشتريات",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_purchaseItems.Count > 0)
            {
                MessageBoxResult result = MessageBox.Show(
                    "هل تريد إلغاء الفاتورة الحالية؟",
                    "تأكيد الإلغاء",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            _purchaseItems.Clear();
            UpdateTotal();
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }

    public class PurchaseItemRow
    {
        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public decimal Total => Quantity * UnitCost;
    }
}
