using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Views;

/// <summary>Database-backed read view for operational sections. Editing workflows are added per feature.</summary>
public sealed class SectionWindow : Window
{
    private readonly AppDbContext _db;
    private readonly string _section;
    private readonly DataGrid _grid = new() { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 8, 0, 0) };

    public SectionWindow(AppDbContext db, string section, string title)
    {
        _db = db; _section = section;
        Title = title; Width = 1050; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var refresh = new Button { Content = "تحديث البيانات", Padding = new Thickness(14, 7, 14, 7), HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += (_, _) => LoadRows();
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(refresh, Dock.Top); root.Children.Add(refresh);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status); root.Children.Add(_grid);
        Content = root;
        _grid.AutoGeneratingColumn += (_, e) =>
        {
            if (_section == "Reports" && ArabicHeaders.TryGetValue(e.PropertyName, out var header))
                e.Column.Header = header;
        };
        Loaded += (_, _) => LoadRows();
    }

    private static readonly Dictionary<string, string> ArabicHeaders = new()
    {
        ["InvoiceNo"] = "رقم الفاتورة", ["SaleDate"] = "التاريخ والوقت", ["SellerCode"] = "كود الموظف",
        ["SellerName"] = "اسم الموظف", ["Subtotal"] = "الإجمالي قبل الخصم", ["Discount"] = "الخصم",
        ["Tax"] = "الضريبة", ["Total"] = "الإجمالي", ["Paid"] = "المدفوع", ["Status"] = "حالة الفاتورة",
        ["PaymentStatus"] = "حالة السداد", ["CustomerPhone"] = "هاتف العميل"
    };

    /// <summary>Completed and cancelled sales with the seller's employee code and name (account name if the user has no employee record).</summary>
    private List<object> LoadSalesRows()
    {
        var sales = _db.Sales.AsNoTracking()
            .Include(s => s.Items).Include(s => s.Payments)
            .Where(s => s.Status != UniversalPOS.Domain.Enums.SaleReturnStatus.Draft)
            .OrderByDescending(s => s.SaleDate).Take(500).ToList();

        var userIds = sales.Where(s => s.UserId.HasValue).Select(s => s.UserId!.Value).Distinct().ToList();
        var users = _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id);
        var employeeIds = users.Values.Where(u => u.EmployeeId.HasValue).Select(u => u.EmployeeId!.Value).Distinct().ToList();
        var employees = _db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionary(e => e.Id);
        bool arabic = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        return sales.Select(s =>
        {
            string code = "-", name = "-";
            if (s.UserId is int uid && users.TryGetValue(uid, out var user))
            {
                name = user.DisplayName;
                if (user.EmployeeId is int eid && employees.TryGetValue(eid, out var emp))
                {
                    code = emp.Code;
                    name = arabic ? emp.ArabicName : emp.EnglishName;
                }
            }

            return (object)new
            {
                InvoiceNo = s.Id,
                s.SaleDate,
                SellerCode = code,
                SellerName = name,
                s.Subtotal,
                Discount = s.DiscountAmount,
                Tax = s.TaxAmount,
                Total = s.TotalAmount,
                Paid = s.PaidAmount,
                s.Status,
                s.PaymentStatus,
                s.CustomerPhone
            };
        }).ToList();
    }

    private void LoadRows()
    {
        try
        {
            object rows = _section switch
            {
                "Products" => _db.Products.AsNoTracking().Select(x => new { x.Id, x.Name, x.Barcode, x.Price, x.CategoryId, x.UnitOfMeasure, x.IsActive, x.ImageKey }).ToList(),
                "LowStock" => (from p in _db.Products.AsNoTracking() join b in _db.StockBalances.AsNoTracking() on p.Id equals b.ProductId where b.Quantity <= 5 select new { p.Id, p.Name, p.Barcode, CurrentQuantity = b.Quantity, ReorderThreshold = 5m, p.UnitOfMeasure }).ToList(),
                "Inventory" => (from p in _db.Products.AsNoTracking() join b in _db.StockBalances.AsNoTracking() on p.Id equals b.ProductId select new { p.Id, p.Name, p.Barcode, Quantity = b.Quantity, p.UnitOfMeasure }).ToList(),
                "Shifts" => _db.CashShifts.AsNoTracking().Select(x => new { x.Id, x.UserId, x.TerminalId, x.OpeningFloat, x.CountedCash, x.ExpectedCash, x.Difference, x.Status, x.CloseNotes }).ToList(),
                "Purchases" => _db.Purchases.AsNoTracking().Select(x => new { x.Id, x.SupplierId, x.Status, x.PurchaseDateUtc, x.SupplierInvoiceNumber }).ToList(),
                "Suppliers" => _db.Suppliers.AsNoTracking().Select(x => new { x.Id, x.Name }).ToList(),
                "Reports" => LoadSalesRows(),
                "Users" => _db.Users.AsNoTracking().Select(x => new { x.Id, x.UserName, x.DisplayName, x.IsActive, x.CreatedAtUtc, x.LastLoginAtUtc }).ToList(),
                _ => new List<object>()
            };
            _grid.ItemsSource = (System.Collections.IEnumerable)rows;
            _status.Text = $"عدد السجلات: {_grid.Items.Count}";
        }
        catch (Exception ex)
        {
            _grid.ItemsSource = null;
            _status.Text = "تعذر تحميل البيانات. راجع اتصال قاعدة البيانات وتوافق الـ Migration مع الموديل.";
            MessageBox.Show(ex.Message, "خطأ في تحميل الشاشة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
