using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Desktop.UI;
using UniversalPOS.Desktop.Views;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Pages;

/// <summary>Products list (mockup 4): picture, barcode, price, quantity, category, status and row actions.</summary>
public sealed class ProductsPage : UserControl
{
    private sealed record ProductRow(int Id, string Code, string Name, string Barcode, decimal Price, decimal CostPrice,
        decimal MinStock, string Unit, string Category, decimal Quantity, string? ImageKey, bool IsActive);

    private readonly AppDbContext _db;
    private IReadOnlyList<ProductRow> _all = Array.Empty<ProductRow>();
    private readonly TextBox _search;
    private readonly ComboBox _categoryFilter;
    private readonly ComboBox _statusFilter;
    private readonly PagedTable<ProductRow> _table;

    public ProductsPage(AppDbContext db)
    {
        _db = db;

        var add = UiKit.PrimaryButton("Plus", UiKit.T("إضافة منتج", "Add product"));
        add.Click += (_, _) => AddProduct();

        _categoryFilter = UiKit.Combo(new[] { UiKit.T("جميع التصنيفات", "All categories") });
        _statusFilter = UiKit.Combo(new[] { UiKit.T("جميع الحالات", "All statuses"), UiKit.T("نشط", "Active"), UiKit.T("غير نشط", "Inactive") });
        var categories = UiKit.ActionButton("Category", UiKit.T("التصنيفات", "Categories"), Brushes.White, UiKit.Ink, UiKit.B("#C9D9EC"));
        categories.Click += (_, _) => ManageCategories();
        var (searchRoot, searchBox) = UiKit.SearchBox(UiKit.T("البحث عن منتج...", "Search for a product..."));
        _search = searchBox;
        _search.TextChanged += (_, _) => ApplyFilter();
        _categoryFilter.SelectionChanged += (_, _) => ApplyFilter();
        _statusFilter.SelectionChanged += (_, _) => ApplyFilter();

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        for (int i = 0; i < 4; i++) toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _categoryFilter.Margin = new Thickness(12, 0, 0, 0);
        _statusFilter.Margin = new Thickness(12, 0, 0, 0);
        categories.Margin = new Thickness(12, 0, 0, 0);
        searchRoot.Margin = new Thickness(12, 0, 0, 0);
        searchRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
        var parts = new FrameworkElement[] { add, _categoryFilter, _statusFilter, categories, searchRoot };
        for (int i = 0; i < parts.Length; i++) { Grid.SetColumn(parts[i], i); toolbar.Children.Add(parts[i]); }

        var columns = new List<TableColumn<ProductRow>>
        {
            new("#", 0.6, (_, index) => UiKit.Text(index.ToString())),
            new(UiKit.T("الصورة", "Image"), 1.0, (p, _) => ImageCell(p)),
            new(UiKit.T("اسم المنتج", "Product"), 2.0, (p, _) => UiKit.Text(p.Name, 14, p.IsActive ? UiKit.Ink : UiKit.Muted, FontWeights.Medium)),
            new(UiKit.T("الباركود", "Barcode"), 1.8, (p, _) => UiKit.Text(p.Barcode)),
            new(UiKit.T("السعر", "Price"), 1.0, (p, _) => UiKit.Text(p.Price.ToString("N2"))),
            new(UiKit.T("الكمية", "Quantity"), 1.0, (p, _) => QuantityCell(p)),
            new(UiKit.T("التصنيف", "Category"), 1.4, (p, _) => UiKit.Text(p.Category)),
            new(UiKit.T("الحالة", "Status"), 1.2, (p, _) => UiKit.Chip(p.IsActive ? UiKit.T("نشط", "Active") : UiKit.T("غير نشط", "Inactive"), p.IsActive ? "green" : "red")),
            new(UiKit.T("الإجراءات", "Actions"), 1.7, (p, _) => ActionsCell(p)),
        };
        _table = new PagedTable<ProductRow>(columns, UiKit.T("منتجات", "products"), UiKit.T("لا توجد منتجات مطابقة.", "No matching products."));

        var stack = new StackPanel();
        stack.Children.Add(UiKit.PageTitle("Box", UiKit.T("المنتجات", "Products")));
        stack.Children.Add(UiKit.Card(new StackPanel { Children = { toolbar, _table } }, 18));
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack, Padding = new Thickness(0, 0, 4, 0) };
        Loaded += (_, _) => LoadRows();
    }

    private static FrameworkElement ImageCell(ProductRow p)
    {
        var bitmap = UiKit.LoadBitmap(UiKit.ProductImagePath(p.ImageKey), 120);
        if (bitmap is null)
            return new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(8), Background = UiKit.B("#EEF3FA"), Child = new Viewbox { Margin = new Thickness(10), Child = UiKit.Icon("Box", 24, UiKit.B("#8CA3C4")) } };
        return new Image { Source = bitmap, Width = 44, Height = 44, Stretch = Stretch.Uniform };
    }

    private static FrameworkElement QuantityCell(ProductRow p)
    {
        var text = UiKit.Text(p.Quantity.ToString("0.##"), 13, p.Quantity <= p.MinStock ? UiKit.B("#C23B3B") : UiKit.Ink, p.Quantity <= p.MinStock ? FontWeights.Bold : FontWeights.Normal);
        text.ToolTip = p.Quantity <= p.MinStock ? UiKit.T("وصل إلى حد إعادة الطلب", "At or below the reorder level") : null;
        return text;
    }

    private FrameworkElement ActionsCell(ProductRow p)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var view = UiKit.IconButton("Eye", UiKit.B("#D9E9FB"), UiKit.B("#1262C4"), UiKit.T("عرض", "View"));
        view.Click += (_, _) => ShowDetails(p);
        var edit = UiKit.IconButton("Edit", UiKit.B("#FDE9C8"), UiKit.B("#C27A0C"), UiKit.T("تعديل", "Edit"));
        edit.Click += (_, _) => EditProduct(p.Id);
        // Products referenced by invoices cannot be deleted; the button switches the product off / on instead.
        var toggle = p.IsActive
            ? UiKit.IconButton("Delete", UiKit.B("#FBDADA"), UiKit.B("#C23B3B"), UiKit.T("تعطيل المنتج", "Deactivate product"))
            : UiKit.IconButton("Check", UiKit.B("#D7F0DE"), UiKit.B("#2B7A43"), UiKit.T("تفعيل المنتج", "Activate product"));
        toggle.Click += (_, _) => Toggle(p.Id);
        panel.Children.Add(view); panel.Children.Add(edit); panel.Children.Add(toggle);
        return panel;
    }

    private Window? Owner => Window.GetWindow(this);

    private void LoadRows()
    {
        try
        {
            var rows = (from p in _db.Products.AsNoTracking()
                        join c in _db.Categories.AsNoTracking() on p.CategoryId equals c.Id
                        select new
                        {
                            p.Id, p.Code, p.Name, p.Barcode, p.Price, p.CostPrice, p.MinStock, p.UnitOfMeasure, Category = c.Name,
                            Quantity = _db.StockBalances.Where(b => b.ProductId == p.Id).Select(b => (decimal?)b.Quantity).FirstOrDefault() ?? 0m,
                            p.ImageKey, p.IsActive
                        }).OrderBy(x => x.Name).ToList();
            _all = rows.Select(x => new ProductRow(x.Id, x.Code, x.Name, x.Barcode, x.Price, x.CostPrice, x.MinStock,
                x.UnitOfMeasure.ToString(), x.Category, x.Quantity, x.ImageKey, x.IsActive)).ToList();

            string? previous = _categoryFilter.SelectedIndex > 0 ? _categoryFilter.SelectedItem?.ToString() : null;
            _categoryFilter.Items.Clear();
            _categoryFilter.Items.Add(UiKit.T("جميع التصنيفات", "All categories"));
            foreach (var name in _all.Select(r => r.Category).Distinct().OrderBy(x => x)) _categoryFilter.Items.Add(name);
            _categoryFilter.SelectedItem = previous is not null && _categoryFilter.Items.Contains(previous) ? previous : _categoryFilter.Items[0];
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _table.SetItems(Array.Empty<ProductRow>());
            MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), UiKit.T("خطأ في تحميل الشاشة", "Screen loading error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        if (_table is null || _all is null) return;
        string term = _search.Text.Trim();
        string? category = _categoryFilter.SelectedIndex > 0 ? _categoryFilter.SelectedItem?.ToString() : null;
        int status = _statusFilter.SelectedIndex;   // 0 all, 1 active, 2 inactive
        var rows = _all.Where(p =>
            (category is null || string.Equals(p.Category, category, StringComparison.CurrentCultureIgnoreCase)) &&
            (status == 0 || (status == 1 && p.IsActive) || (status == 2 && !p.IsActive)) &&
            (term.Length == 0 ||
             p.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
             p.Barcode.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
             p.Code.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToList();
        _table.SetItems(rows);
    }

    private void ShowDetails(ProductRow p)
    {
        string text = string.Join(Environment.NewLine, new[]
        {
            $"{UiKit.T("الكود", "Code")}: {p.Code}",
            $"{UiKit.T("اسم المنتج", "Product")}: {p.Name}",
            $"{UiKit.T("الباركود", "Barcode")}: {p.Barcode}",
            $"{UiKit.T("التصنيف", "Category")}: {p.Category}",
            $"{UiKit.T("سعر البيع", "Sale price")}: {p.Price:N2}",
            $"{UiKit.T("سعر التكلفة", "Cost price")}: {p.CostPrice:N2}",
            $"{UiKit.T("الكمية المتاحة", "Quantity")}: {p.Quantity:0.##} {p.Unit}",
            $"{UiKit.T("حد إعادة الطلب", "Reorder level")}: {p.MinStock:0.##}",
            $"{UiKit.T("الحالة", "Status")}: {(p.IsActive ? UiKit.T("نشط", "Active") : UiKit.T("غير نشط", "Inactive"))}"
        });
        MessageBox.Show(text, UiKit.T("بيانات المنتج", "Product details"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void EditProduct(int id)
    {
        try
        {
            var product = _db.Products.SingleOrDefault(x => x.Id == id);
            if (product is null) { LoadRows(); return; }
            var editor = new ProductEditorWindow(_db, product) { Owner = Owner };
            if (editor.ShowDialog() != true) return;
            _db.SaveChanges();
            LoadRows();
            MessageBox.Show(UiKit.T("تم حفظ تعديلات المنتج دون تغيير رصيد المخزون.", "Product changes saved without changing stock balance."),
                UiKit.T("المنتجات", "Products"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(UiKit.T("تعذر تعديل المنتج: ", "Could not edit product: ") + ex.GetBaseException().Message,
                UiKit.T("خطأ", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Toggle(int id)
    {
        try
        {
            var product = _db.Products.SingleOrDefault(x => x.Id == id);
            if (product is null) { LoadRows(); return; }
            string action = product.IsActive ? UiKit.T("تعطيل", "deactivate") : UiKit.T("تفعيل", "activate");
            var answer = MessageBox.Show(
                UiKit.T($"هل تريد {action} المنتج «{product.Name}»؟", $"Do you want to {action} product '{product.Name}'?"),
                UiKit.T("تأكيد تغيير الحالة", "Confirm status change"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            if (product.IsActive) product.Deactivate(); else product.Activate();
            _db.SaveChanges();
            LoadRows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(UiKit.T("تعذر تغيير حالة المنتج: ", "Could not change product status: ") + ex.GetBaseException().Message,
                UiKit.T("خطأ", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ManageCategories()
    {
        try
        {
            new CategoryManagementWindow(_db) { Owner = Owner }.ShowDialog();
            LoadRows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(UiKit.T("تعذر فتح إدارة التصنيفات: ", "Could not open category management: ") + ex.GetBaseException().Message,
                UiKit.T("التصنيفات", "Categories"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddProduct()
    {
        try
        {
            var editor = new ProductEditorWindow(_db) { Owner = Owner };
            if (editor.ShowDialog() != true) return;
            var product = editor.CreatedProduct;
            if (product is null) return;

            using var transaction = _db.Database.BeginTransaction();
            _db.Products.Add(product);
            _db.SaveChanges();
            if (!string.IsNullOrWhiteSpace(editor.SelectedImagePath))
            {
                var sourceImage = new FileInfo(editor.SelectedImagePath);
                var extension = sourceImage.Extension.ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
                    throw new InvalidOperationException(UiKit.T("صيغة الصورة غير مدعومة.", "Unsupported image format."));
                if (sourceImage.Length > 5 * 1024 * 1024)
                    throw new InvalidOperationException(UiKit.T("حجم الصورة يتجاوز 5 ميجابايت.", "Image exceeds 5 MB."));
                var imageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS", "ProductImages");
                var relativeImageKey = $"products/{product.Id}/{Guid.NewGuid():N}{(extension == ".jpeg" ? ".jpg" : extension)}";
                var destination = Path.Combine(imageRoot, relativeImageKey.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(editor.SelectedImagePath, destination, overwrite: false);
                product.SetImageKey(relativeImageKey);
                _db.SaveChanges();
            }
            _db.StockBalances.Add(new StockBalance(product.Id, editor.OpeningQuantity));
            if (editor.OpeningQuantity > 0m)
            {
                var session = App.Services.GetRequiredService<CurrentUserSession>();
                if (!session.UserId.HasValue)
                    throw new InvalidOperationException(UiKit.T("يجب تسجيل الدخول قبل تسجيل رصيد افتتاحي.", "Sign in before recording opening stock."));
                _db.StockMovements.Add(new StockMovement(product.Id, StockMovementType.AdjustmentIncrease,
                    editor.OpeningQuantity, "OpeningStock", product.Id.ToString(),
                    UiKit.T("رصيد افتتاحي عند إنشاء المنتج", "Opening stock when product was created"), session.UserId.Value));
            }
            _db.SaveChanges();
            transaction.Commit();
            LoadRows();
            MessageBox.Show(UiKit.T("تمت إضافة المنتج بنجاح.", "Product added successfully."),
                UiKit.T("المنتجات", "Products"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(UiKit.T($"تعذرت إضافة المنتج: {ex.GetBaseException().Message}", $"Could not add product: {ex.GetBaseException().Message}"),
                UiKit.T("خطأ", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
