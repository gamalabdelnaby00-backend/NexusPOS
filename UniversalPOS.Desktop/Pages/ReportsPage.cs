using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Desktop.UI;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Pages;

/// <summary>Reports dashboard (mockup 1): KPI cards, 7-day sales chart, sales by category and recent transactions.</summary>
public sealed class ReportsPage : UserControl
{
    private sealed record Transaction(DateTime Date, bool IsSale, string Statement, decimal Amount);

    private const int PeriodDays = 30;
    private readonly AppDbContext _db;
    private readonly StackPanel _root = new();

    public ReportsPage(AppDbContext db)
    {
        _db = db;
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _root, Padding = new Thickness(0, 0, 4, 0) };
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        _root.Children.Clear();
        try
        {
            var today = DateTime.Today;
            var from = today.AddDays(-(PeriodDays - 1));
            var previousFrom = today.AddDays(-(2 * PeriodDays - 1));

            // Completed sales only (drafts and cancelled invoices never count). Two periods are loaded so each KPI can show the change.
            var sales = _db.Sales.AsNoTracking().Include(s => s.Items)
                .Where(s => s.Status == SaleReturnStatus.Completed && s.SaleDate >= previousFrom).ToList();
            var products = _db.Products.AsNoTracking().Select(p => new { p.Id, p.CostPrice, p.CategoryId, p.IsActive }).ToList();
            var productById = products.ToDictionary(p => p.Id);
            var categories = _db.Categories.AsNoTracking().ToDictionary(c => c.Id, c => c.Name);

            var previousFromUtc = previousFrom.ToUniversalTime();
            var purchases = (from p in _db.Purchases.AsNoTracking()
                             where p.Status == PurchaseStatus.Received && p.PurchaseDateUtc >= previousFromUtc
                             select new
                             {
                                 p.Id, p.PurchaseDateUtc, p.SupplierInvoiceNumber,
                                 Total = _db.PurchaseItems.Where(i => EF.Property<int>(i, "PurchaseId") == p.Id)
                                     .Select(i => (decimal?)(i.UnitCost * i.Quantity)).Sum() ?? 0m
                             }).ToList();

            var currentSales = sales.Where(s => s.SaleDate >= from).ToList();
            var earlierSales = sales.Where(s => s.SaleDate < from).ToList();
            decimal salesNow = currentSales.Sum(s => s.TotalAmount), salesBefore = earlierSales.Sum(s => s.TotalAmount);

            decimal Cost(IEnumerable<Sale> list) => list.SelectMany(s => s.Items)
                .Sum(i => productById.TryGetValue(i.ProductId, out var p) ? p.CostPrice * i.Quantity : 0m);
            // Profit estimate: sales minus the cost price (as currently set on each product) of the goods sold.
            decimal profitNow = salesNow - Cost(currentSales), profitBefore = salesBefore - Cost(earlierSales);

            var currentPurchases = purchases.Where(p => p.PurchaseDateUtc.ToLocalTime() >= from).ToList();
            decimal purchasesNow = currentPurchases.Sum(p => p.Total);
            decimal purchasesBefore = purchases.Where(p => p.PurchaseDateUtc.ToLocalTime() < from).Sum(p => p.Total);

            int productCount = products.Count, activeCount = products.Count(p => p.IsActive);

            // ---- header card with the four KPI cards (right to left: products, profit, purchases, sales)
            var kpis = new UniformGrid { Columns = 4, Margin = new Thickness(0, 14, 0, 0) };
            kpis.Children.Add(Kpi("Box", UiKit.T("إجمالي المنتجات", "Total products"), productCount.ToString("N0"),
                UiKit.T($"{activeCount:N0} نشط", $"{activeCount:N0} active"), null, "#6B46C1", "#D5C8F0", "#FAF8FF", "#E7DEFA"));
            kpis.Children.Add(Kpi("Trend", UiKit.T("صافي الربح", "Net profit"), profitNow.ToString("N2"),
                null, Change(profitNow, profitBefore), "#D9780B", "#F6D9A8", "#FFFBF3", "#FBE8C6"));
            kpis.Children.Add(Kpi("Receipt", UiKit.T("إجمالي المشتريات", "Total purchases"), purchasesNow.ToString("N2"),
                null, Change(purchasesNow, purchasesBefore), "#2B8A57", "#BFE5CF", "#F6FCF8", "#D8F1E2"));
            kpis.Children.Add(Kpi("Bag", UiKit.T("إجمالي المبيعات", "Total sales"), salesNow.ToString("N2"),
                null, Change(salesNow, salesBefore), "#1F5FBF", "#BBD3F2", "#F7FAFF", "#DCEAFB"));

            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock { Text = UiKit.T("التقارير", "Reports"), FontSize = 28, FontWeight = FontWeights.Bold, Foreground = UiKit.DeepBlue });
            headerStack.Children.Add(new TextBlock
            {
                Text = UiKit.T($"آخر {PeriodDays} يومًا مقارنة بالفترة السابقة. الربح تقديري حسب سعر التكلفة الحالي للمنتجات.",
                               $"Last {PeriodDays} days compared with the previous period. Profit is estimated from the current product cost prices."),
                FontSize = 12, Foreground = UiKit.Muted, Margin = new Thickness(0, 4, 0, 0)
            });
            headerStack.Children.Add(kpis);
            _root.Children.Add(UiKit.Card(headerStack, 20));

            // ---- bottom row: recent transactions | sales by category | last 7 days
            var bottom = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });

            var recent = sales.Select(s => new Transaction(s.SaleDate, true, UiKit.T($"فاتورة بيع #{s.Id}", $"Sales invoice #{s.Id}"), s.TotalAmount))
                .Concat(purchases.Select(p => new Transaction(p.PurchaseDateUtc.ToLocalTime(), false,
                    UiKit.T($"فاتورة شراء {(string.IsNullOrWhiteSpace(p.SupplierInvoiceNumber) ? $"PO-{p.Id:D4}" : p.SupplierInvoiceNumber)}",
                            $"Purchase invoice {(string.IsNullOrWhiteSpace(p.SupplierInvoiceNumber) ? $"PO-{p.Id:D4}" : p.SupplierInvoiceNumber)}"), p.Total)))
                .OrderByDescending(t => t.Date).Take(5).ToList();

            var transactionsCard = SectionCard(UiKit.T("أحدث المعاملات", "Recent transactions"), TransactionsTable(recent));
            transactionsCard.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetColumn(transactionsCard, 0);

            // sales by category (current period)
            var byCategory = currentSales.SelectMany(s => s.Items)
                .GroupBy(i => productById.TryGetValue(i.ProductId, out var p) && categories.TryGetValue(p.CategoryId, out var name) ? name : UiKit.T("غير مصنف", "Uncategorised"))
                .Select(g => (Label: g.Key, Value: (double)g.Sum(i => i.TotalPrice))).OrderByDescending(x => x.Value).ToList();
            var slices = byCategory.Take(4).ToList();
            double rest = byCategory.Skip(4).Sum(x => x.Value);
            if (rest > 0) slices.Add((UiKit.T("أخرى", "Other"), rest));
            var pieCard = SectionCard(UiKit.T("توزيع المبيعات حسب الفئات", "Sales by category"), PieWithLegend(slices));
            pieCard.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(pieCard, 1);

            // last 7 days
            var days = Enumerable.Range(0, 7).Select(i => today.AddDays(-(6 - i))).ToList();
            var series = days.Select(d => (d.ToString("dd/MM"),
                (double)sales.Where(s => s.SaleDate.Date == d).Sum(s => s.TotalAmount))).ToList();
            var chart = new AreaLineChart { Height = 250, Margin = new Thickness(0, 6, 0, 0) };
            chart.SetPoints(series);
            var lineCard = SectionCard(UiKit.T("المبيعات خلال آخر 7 أيام", "Sales in the last 7 days"), chart);
            lineCard.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(lineCard, 2);

            bottom.Children.Add(transactionsCard); bottom.Children.Add(pieCard); bottom.Children.Add(lineCard);
            _root.Children.Add(bottom);
        }
        catch (Exception ex)
        {
            _root.Children.Add(new TextBlock
            {
                Text = ErrorMessageMapper.ToUserMessage(ex), Foreground = Brushes.Firebrick, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
            });
        }
    }

    private static (string Text, bool Up)? Change(decimal now, decimal before)
    {
        if (before == 0m) return now == 0m ? ("0%", true) : null;
        decimal pct = (now - before) / Math.Abs(before) * 100m;
        return ((pct >= 0 ? "+" : "") + pct.ToString("0.#") + "%", pct >= 0);
    }

    private static Border Kpi(string icon, string title, string value, string? note, (string Text, bool Up)? change,
        string accent, string border, string background, string iconBg)
    {
        var accentBrush = UiKit.B(accent);
        var iconBox = new Border
        {
            Width = 50, Height = 50, CornerRadius = new CornerRadius(12), Background = UiKit.B(iconBg), VerticalAlignment = VerticalAlignment.Top,
            Child = new Viewbox { Margin = new Thickness(12), Child = UiKit.Icon(icon, 26, accentBrush) }
        };
        var texts = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        texts.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = accentBrush });
        texts.Children.Add(new TextBlock { Text = value, FontSize = 30, FontWeight = FontWeights.Bold, Foreground = accentBrush, Margin = new Thickness(0, 4, 0, 0) });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(iconBox);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        FrameworkElement? badge = null;
        if (change is { } c)
        {
            var color = c.Up ? "#2B7A43" : "#C23B3B";
            var chipBg = c.Up ? "#E3F5E8" : "#FDE3E3";
            var inner = new StackPanel { Orientation = Orientation.Horizontal };
            inner.Children.Add(UiKit.Icon(c.Up ? "ArrowUp" : "ArrowDown", 16, UiKit.B(color)));
            inner.Children.Add(new TextBlock { Text = c.Text, Foreground = UiKit.B(color), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            badge = new Border { Background = UiKit.B(chipBg), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 10, 3), Child = inner };
        }
        else if (note is not null)
        {
            badge = new Border
            {
                Background = UiKit.B("#EFE9FB"), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 10, 3),
                Child = new TextBlock { Text = note, Foreground = accentBrush, FontSize = 12, FontWeight = FontWeights.SemiBold }
            };
        }
        if (badge is not null)
        {
            badge.HorizontalAlignment = HorizontalAlignment.Right;
            badge.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetRow(badge, 1); Grid.SetColumnSpan(badge, 2);
            grid.Children.Add(badge);
        }

        return new Border
        {
            Background = UiKit.B(background), BorderBrush = UiKit.B(border), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16), Margin = new Thickness(6, 0, 6, 0), Child = grid
        };
    }

    private static Border SectionCard(string title, UIElement body)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = UiKit.Ink });
        stack.Children.Add(new Border { Height = 1, Background = UiKit.Line, Margin = new Thickness(0, 10, 0, 12) });
        stack.Children.Add(body);
        return UiKit.Card(stack, 18);
    }

    private static UIElement PieWithLegend(IReadOnlyList<(string Label, double Value)> slices)
    {
        double total = slices.Sum(s => s.Value);
        if (total <= 0)
            return new TextBlock { Text = UiKit.T("لا توجد مبيعات في هذه الفترة.", "No sales in this period."), Foreground = UiKit.Muted, Margin = new Thickness(0, 40, 0, 40), HorizontalAlignment = HorizontalAlignment.Center };

        var pie = new PieChart { Width = 160, Height = 160, VerticalAlignment = VerticalAlignment.Center };
        pie.SetSlices(slices);

        var legend = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < slices.Count; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dot = new System.Windows.Shapes.Ellipse { Width = 11, Height = 11, Fill = UiKit.B(PieChart.Palette[i % PieChart.Palette.Length]), VerticalAlignment = VerticalAlignment.Center };
            var name = UiKit.Text(slices[i].Label, 13);
            name.Margin = new Thickness(8, 0, 12, 0);
            var pct = UiKit.Text((slices[i].Value / total).ToString("0%"), 13, UiKit.Muted);
            Grid.SetColumn(name, 1); Grid.SetColumn(pct, 2);
            row.Children.Add(dot); row.Children.Add(name); row.Children.Add(pct);
            legend.Children.Add(row);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(legend);
        Grid.SetColumn(pie, 1);
        grid.Children.Add(pie);
        return grid;
    }

    private static UIElement TransactionsTable(IReadOnlyList<Transaction> rows)
    {
        var widths = new[] { 1.2, 0.9, 1.9, 1.1 };
        Grid Row(Brush? background)
        {
            var g = new Grid { Background = background, MinHeight = 38 };
            foreach (var w in widths) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
            return g;
        }
        void Put(Grid g, int col, string text, bool bold = false, Brush? fg = null)
        {
            var t = UiKit.Text(text, 12.5, fg ?? UiKit.Ink, bold ? FontWeights.SemiBold : FontWeights.Normal);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(6, 8, 6, 8);
            Grid.SetColumn(t, col);
            g.Children.Add(t);
        }

        var stack = new StackPanel();
        var header = Row(UiKit.TableHeader);
        Put(header, 0, UiKit.T("التاريخ", "Date"), true); Put(header, 1, UiKit.T("النوع", "Type"), true);
        Put(header, 2, UiKit.T("البيان", "Details"), true); Put(header, 3, UiKit.T("المبلغ", "Amount"), true);
        stack.Children.Add(header);
        if (rows.Count == 0)
            stack.Children.Add(new TextBlock { Text = UiKit.T("لا توجد معاملات بعد.", "No transactions yet."), Foreground = UiKit.Muted, Margin = new Thickness(0, 24, 0, 24), HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var t in rows)
        {
            var g = Row(null);
            Put(g, 0, t.Date.ToString("yyyy/MM/dd"));
            Put(g, 1, t.IsSale ? UiKit.T("مبيعات", "Sales") : UiKit.T("مشتريات", "Purchases"));
            Put(g, 2, t.Statement);
            Put(g, 3, t.Amount.ToString("N2"));
            stack.Children.Add(new Border { BorderBrush = UiKit.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = g });
        }
        return new Border { BorderBrush = UiKit.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = stack };
    }
}
