using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using NexusPOS;
using UniversalPOS.Desktop.UI;
using UniversalPOS.Desktop.Views;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Pages;

/// <summary>Purchase invoices list (mockup 2): supplier / status filters, search and view / cancel actions.</summary>
public sealed class PurchasesPage : UserControl
{
    private sealed record PurchaseRow(int Id, string Invoice, string Supplier, DateTime Date, decimal Total, PurchaseStatus Status);

    private readonly AppDbContext _db;
    private IReadOnlyList<PurchaseRow> _all = Array.Empty<PurchaseRow>();
    private readonly TextBox _search;
    private readonly ComboBox _supplierFilter;
    private readonly ComboBox _statusFilter;
    private readonly PagedTable<PurchaseRow> _table;

    public PurchasesPage(AppDbContext db)
    {
        _db = db;

        var add = UiKit.PrimaryButton("Plus", UiKit.T("إضافة مشتريات", "Add purchase"));
        add.Click += (_, _) => AddPurchase();

        _supplierFilter = UiKit.Combo(new[] { UiKit.T("جميع الموردين", "All suppliers") });
        _statusFilter = UiKit.Combo(new[]
        {
            UiKit.T("جميع الحالات", "All statuses"), StatusText(PurchaseStatus.Received), StatusText(PurchaseStatus.Draft), StatusText(PurchaseStatus.Cancelled)
        });
        var (searchRoot, searchBox) = UiKit.SearchBox(UiKit.T("البحث عن فاتورة...", "Search for an invoice..."));
        _search = searchBox;
        _search.TextChanged += (_, _) => ApplyFilter();
        _supplierFilter.SelectionChanged += (_, _) => ApplyFilter();
        _statusFilter.SelectionChanged += (_, _) => ApplyFilter();

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        for (int i = 0; i < 3; i++) toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _supplierFilter.Margin = new Thickness(12, 0, 0, 0);
        _statusFilter.Margin = new Thickness(12, 0, 0, 0);
        searchRoot.Margin = new Thickness(12, 0, 0, 0);
        searchRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
        var parts = new FrameworkElement[] { add, _supplierFilter, _statusFilter, searchRoot };
        for (int i = 0; i < parts.Length; i++) { Grid.SetColumn(parts[i], i); toolbar.Children.Add(parts[i]); }

        var columns = new List<TableColumn<PurchaseRow>>
        {
            new("#", 0.6, (_, index) => UiKit.Text(index.ToString())),
            new(UiKit.T("رقم الفاتورة", "Invoice no."), 1.8, (p, _) => UiKit.Text(p.Invoice)),
            new(UiKit.T("اسم المورد", "Supplier"), 2.0, (p, _) => UiKit.Text(p.Supplier, 14, UiKit.Ink, FontWeights.Medium)),
            new(UiKit.T("التاريخ", "Date"), 1.4, (p, _) => UiKit.Text(p.Date.ToLocalTime().ToString("yyyy/MM/dd"))),
            new(UiKit.T("الإجمالي", "Total"), 1.4, (p, _) => UiKit.Text(p.Total.ToString("N2"))),
            new(UiKit.T("الحالة", "Status"), 1.5, (p, _) => UiKit.Chip(StatusText(p.Status), StatusKind(p.Status))),
            new(UiKit.T("الإجراءات", "Actions"), 1.7, (p, _) => ActionsCell(p)),
        };
        _table = new PagedTable<PurchaseRow>(columns, UiKit.T("مشتريات", "purchases"), UiKit.T("لا توجد فواتير مطابقة.", "No matching invoices."));

        var stack = new StackPanel();
        stack.Children.Add(UiKit.PageTitle("Doc", UiKit.T("المشتريات", "Purchases")));
        stack.Children.Add(UiKit.Card(new StackPanel { Children = { toolbar, _table } }, 18));
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack, Padding = new Thickness(0, 0, 4, 0) };
        Loaded += (_, _) => LoadRows();
    }

    private static string StatusText(PurchaseStatus s) => s switch
    {
        PurchaseStatus.Received => UiKit.T("مكتملة", "Completed"),
        PurchaseStatus.Draft => UiKit.T("قيد المعالجة", "Processing"),
        _ => UiKit.T("ملغاة", "Cancelled")
    };

    private static string StatusKind(PurchaseStatus s) => s switch
    {
        PurchaseStatus.Received => "green",
        PurchaseStatus.Draft => "orange",
        _ => "red"
    };

    private Window? Owner => Window.GetWindow(this);

    private FrameworkElement ActionsCell(PurchaseRow p)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var view = UiKit.IconButton("Eye", UiKit.B("#D9E9FB"), UiKit.B("#1262C4"), UiKit.T("عرض", "View"));
        view.Click += (_, _) => ShowDetails(p);
        // Received invoices already changed stock, so they are never edited in place; the button stays visible but disabled.
        var edit = UiKit.IconButton("Edit", UiKit.B("#FDE9C8"), UiKit.B("#C27A0C"),
            UiKit.T("لا يمكن تعديل الفاتورة بعد حفظها", "Saved invoices cannot be edited"));
        edit.IsEnabled = false;
        var cancel = UiKit.IconButton("Delete", UiKit.B("#FBDADA"), UiKit.B("#C23B3B"),
            p.Status == PurchaseStatus.Draft ? UiKit.T("إلغاء الفاتورة", "Cancel invoice") : UiKit.T("يمكن إلغاء الفواتير قيد المعالجة فقط", "Only invoices in processing can be cancelled"));
        cancel.IsEnabled = p.Status == PurchaseStatus.Draft;
        cancel.Click += (_, _) => Cancel(p);
        panel.Children.Add(view); panel.Children.Add(edit); panel.Children.Add(cancel);
        return panel;
    }

    private void LoadRows()
    {
        try
        {
            var rows = (from p in _db.Purchases.AsNoTracking()
                        join s in _db.Suppliers.AsNoTracking() on p.SupplierId equals s.Id
                        select new
                        {
                            p.Id, p.SupplierInvoiceNumber, Supplier = s.Name, p.PurchaseDateUtc, p.Status,
                            Total = _db.PurchaseItems.Where(i => EF.Property<int>(i, "PurchaseId") == p.Id)
                                .Select(i => (decimal?)(i.UnitCost * i.Quantity)).Sum() ?? 0m
                        }).OrderByDescending(x => x.PurchaseDateUtc).ToList();
            _all = rows.Select(x => new PurchaseRow(x.Id,
                string.IsNullOrWhiteSpace(x.SupplierInvoiceNumber) ? $"PO-{x.Id:D4}" : x.SupplierInvoiceNumber!,
                x.Supplier, x.PurchaseDateUtc, x.Total, x.Status)).ToList();

            string? previous = _supplierFilter.SelectedIndex > 0 ? _supplierFilter.SelectedItem?.ToString() : null;
            _supplierFilter.Items.Clear();
            _supplierFilter.Items.Add(UiKit.T("جميع الموردين", "All suppliers"));
            foreach (var name in _all.Select(r => r.Supplier).Distinct().OrderBy(x => x)) _supplierFilter.Items.Add(name);
            _supplierFilter.SelectedItem = previous is not null && _supplierFilter.Items.Contains(previous) ? previous : _supplierFilter.Items[0];
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _table.SetItems(Array.Empty<PurchaseRow>());
            MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), UiKit.T("خطأ في تحميل الشاشة", "Screen loading error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        if (_table is null || _all is null) return;
        string term = _search.Text.Trim();
        string? supplier = _supplierFilter.SelectedIndex > 0 ? _supplierFilter.SelectedItem?.ToString() : null;
        PurchaseStatus? status = _statusFilter.SelectedIndex switch
        {
            1 => PurchaseStatus.Received, 2 => PurchaseStatus.Draft, 3 => PurchaseStatus.Cancelled, _ => null
        };
        var rows = _all.Where(p =>
            (supplier is null || string.Equals(p.Supplier, supplier, StringComparison.CurrentCultureIgnoreCase)) &&
            (status is null || p.Status == status) &&
            (term.Length == 0 ||
             p.Invoice.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
             p.Supplier.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToList();
        _table.SetItems(rows);
    }

    private void AddPurchase()
    {
        try
        {
            var editor = new PurchaseEditorWindow { Owner = Owner };
            if (editor.ShowDialog() == true)
            {
                LoadRows();
                MessageBox.Show(UiKit.T("تم حفظ فاتورة الشراء واستلام الأصناف وتحديث المخزون.", "Purchase invoice saved, received and stock updated."),
                    UiKit.T("المشتريات", "Purchases"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(UiKit.T("تعذر فتح فاتورة الشراء: ", "Could not open purchase invoice: ") + ex.GetBaseException().Message,
                UiKit.T("خطأ", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowDetails(PurchaseRow row)
    {
        try
        {
            var lines = (from i in _db.PurchaseItems.AsNoTracking()
                         join pr in _db.Products.AsNoTracking() on i.ProductId equals pr.Id
                         where EF.Property<int>(i, "PurchaseId") == row.Id
                         select new { pr.Name, i.Quantity, i.UnitCost }).ToList();
            var text = new System.Text.StringBuilder();
            text.AppendLine($"{UiKit.T("رقم الفاتورة", "Invoice no.")}: {row.Invoice}");
            text.AppendLine($"{UiKit.T("المورد", "Supplier")}: {row.Supplier}");
            text.AppendLine($"{UiKit.T("التاريخ", "Date")}: {row.Date.ToLocalTime():yyyy/MM/dd}");
            text.AppendLine($"{UiKit.T("الحالة", "Status")}: {StatusText(row.Status)}");
            text.AppendLine();
            foreach (var l in lines)
                text.AppendLine($"• {l.Name}  ×  {l.Quantity:0.##}  @  {l.UnitCost:N2}  =  {l.Quantity * l.UnitCost:N2}");
            text.AppendLine();
            text.AppendLine($"{UiKit.T("الإجمالي", "Total")}: {row.Total:N2}");
            MessageBox.Show(text.ToString(), UiKit.T("تفاصيل فاتورة الشراء", "Purchase details"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), UiKit.T("خطأ", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel(PurchaseRow row)
    {
        var answer = MessageBox.Show(
            UiKit.T($"هل تريد إلغاء فاتورة الشراء {row.Invoice}؟", $"Cancel purchase invoice {row.Invoice}?"),
            UiKit.T("تأكيد الإلغاء", "Confirm cancellation"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            var purchase = _db.Purchases.SingleOrDefault(p => p.Id == row.Id);
            if (purchase is null) { LoadRows(); return; }
            purchase.Cancel();
            _db.SaveChanges();
            LoadRows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, UiKit.T("تعذر الإلغاء", "Could not cancel"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
