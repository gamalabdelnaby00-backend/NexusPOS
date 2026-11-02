
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Views;

/// <summary>
/// Physical stock count using the existing stock-adjustment entities and tables.
/// </summary>
public sealed partial class StocktakingWindow : Window
{
    private readonly AppDbContext _db;
    private readonly DataGrid _grid = new()
    {
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        IsReadOnly = false,
        RowHeight = 42,
        Margin = new Thickness(0, 12, 0, 12),
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        SelectionMode = DataGridSelectionMode.Single,
        SelectionUnit = DataGridSelectionUnit.FullRow
    };

    private readonly TextBox _reason = new()
    {
        MinWidth = 260,
        Height = 40,
        Padding = new Thickness(10),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private readonly TextBox _search = new()
    {
        MinWidth = 230,
        Height = 40,
        Padding = new Thickness(10),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private readonly ComboBox _filter = new()
    {
        MinWidth = 170,
        Height = 40,
        Padding = new Thickness(8),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private readonly TextBlock _summary = new()
    {
        Margin = new Thickness(0, 12, 0, 4),
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap
    };

    private readonly List<CountRow> _rows;
    private ICollectionView _view = null!;
    private Button _saveButton = null!;
    private bool _isSaving;
    private readonly bool _arabic;

    private static readonly Brush Navy =
        new SolidColorBrush(Color.FromRgb(20, 43, 72));

    private static readonly Brush Blue =
        new SolidColorBrush(Color.FromRgb(22, 105, 232));

    private static readonly Brush Green =
        new SolidColorBrush(Color.FromRgb(21, 130, 80));

    private static readonly Brush Red =
        new SolidColorBrush(Color.FromRgb(190, 55, 55));

    public StocktakingWindow(AppDbContext db, bool arabic)
    {
        _db = db;
        _arabic = arabic;

        Title = arabic ? "الجرد وتسوية المخزون" : "Stocktaking & Adjustment";
        Width = 1120;
        Height = 760;
        MinWidth = 850;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 247, 252));
        FontFamily = new FontFamily("Segoe UI");
        FlowDirection = arabic
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        var data =
            (from p in _db.Products.AsNoTracking()
             join b in _db.StockBalances.AsNoTracking()
                 on p.Id equals b.ProductId
             orderby p.Name
             select new
             {
                 p.Id,
                 p.Code,
                 p.Name,
                 SystemQuantity = b.Quantity,
                 p.UnitOfMeasure
             }).ToList();

        _rows = data.Select(x => new CountRow
        {
            ProductId = x.Id,
            Code = x.Code,
            Name = x.Name,
            SystemQuantity = x.SystemQuantity,
            CountedQuantity = x.SystemQuantity,
            Unit = x.UnitOfMeasure.ToString()
        }).ToList();

        ConfigureGrid();

        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = FilterRow;
        _grid.ItemsSource = _view;

        _search.TextChanged += (_, _) => RefreshGrid();
        _filter.SelectionChanged += (_, _) => RefreshGrid();

        var title = new TextBlock
        {
            Text = Title,
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            Foreground = Navy,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var description = new TextBlock
        {
            Text = arabic
                ? "راجع رصيد النظام، أدخل الكمية الفعلية، ثم راجع الفروق قبل اعتماد التسوية."
                : "Review system quantities, enter counted quantities, and check differences before approval.",
            Foreground = new SolidColorBrush(Color.FromRgb(104, 119, 140)),
            TextWrapping = TextWrapping.Wrap
        };

        var searchLabel = new TextBlock
        {
            Text = arabic ? "بحث" : "Search",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        _search.ToolTip = arabic
            ? "ابحث باسم الصنف أو كود الصنف"
            : "Search by product name or code";

        _search.Text = "";

        var searchRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 16, 0, 4)
        };

        searchRow.Children.Add(searchLabel);
        searchRow.Children.Add(_search);

        _filter.Items.Add(new ComboBoxItem
        {
            Content = arabic ? "كل الأصناف" : "All products",
            Tag = "all"
        });

        _filter.Items.Add(new ComboBoxItem
        {
            Content = arabic ? "أصناف بها عجز" : "Shortage",
            Tag = "shortage"
        });

        _filter.Items.Add(new ComboBoxItem
        {
            Content = arabic ? "أصناف بها زيادة" : "Surplus",
            Tag = "surplus"
        });

        _filter.Items.Add(new ComboBoxItem
        {
            Content = arabic ? "بدون فرق" : "No difference",
            Tag = "same"
        });

        _filter.SelectedIndex = 0;

        var filterLabel = new TextBlock
        {
            Text = arabic ? "تصفية:" : "Filter:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18, 0, 8, 0)
        };

        searchRow.Children.Add(filterLabel);
        searchRow.Children.Add(_filter);

        var reasonLabel = new TextBlock
        {
            Text = arabic ? "سبب الجرد / التسوية" : "Count / adjustment reason",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var reasonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 4)
        };

        reasonRow.Children.Add(reasonLabel);
        reasonRow.Children.Add(_reason);

        _saveButton = new Button
        {
            Content = arabic
                ? "اعتماد التسوية وتحديث المخزون"
                : "Approve adjustment & update stock",
            Background = Blue,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(18, 12, 18, 12),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            MinHeight = 42
        };

        _saveButton.Click += SaveAdjustment;

        var cancel = new Button
        {
            Content = arabic ? "إغلاق" : "Close",
            Padding = new Thickness(20, 12, 20, 12),
            Margin = new Thickness(10, 0, 0, 0),
            Background = Brushes.White,
            MinHeight = 42,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        cancel.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(_saveButton);

        var top = new StackPanel();
        top.Children.Add(title);
        top.Children.Add(description);
        top.Children.Add(searchRow);
        top.Children.Add(reasonRow);
        top.Children.Add(_summary);

        var layout = new DockPanel
        {
            Margin = new Thickness(20)
        };

        DockPanel.SetDock(top, Dock.Top);
        layout.Children.Add(top);

        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(_grid);

        Content = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(8),
            Margin = new Thickness(16),
            Child = layout
        };

        _grid.CellEditEnding += (_, _) =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _view.Refresh();
                UpdateSummary();
            }));

        UpdateSummary();
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = _arabic ? "كود الصنف" : "Code",
            Binding = new Binding(nameof(CountRow.Code)),
            IsReadOnly = true,
            Width = 115
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = _arabic ? "اسم الصنف" : "Product",
            Binding = new Binding(nameof(CountRow.Name)),
            IsReadOnly = true,
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = _arabic ? "رصيد النظام" : "System qty",
            Binding = new Binding(nameof(CountRow.SystemQuantity))
            {
                StringFormat = "N3"
            },
            IsReadOnly = true,
            Width = 130
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = _arabic ? "الكمية الفعلية" : "Counted qty",
            Binding = new Binding(nameof(CountRow.CountedQuantity))
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                StringFormat = "N3"
            },
            Width = 145
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = _arabic ? "الفرق" : "Difference",
            Binding = new Binding(nameof(CountRow.Difference))
            {
                StringFormat = "+0.###;-0.###;0"
            },
            IsReadOnly = true,
            Width = 100
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = _arabic ? "الوحدة" : "Unit",
            Binding = new Binding(nameof(CountRow.Unit)),
            IsReadOnly = true,
            Width = 90
        });

        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(
            DataGridCell.PaddingProperty,
            new Thickness(8, 4, 8, 4)));

        _grid.CellStyle = style;

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(
            DataGridRow.BackgroundProperty,
            Brushes.White));

        rowStyle.Setters.Add(new Setter(
            DataGridRow.BorderBrushProperty,
            new SolidColorBrush(Color.FromRgb(235, 240, 247))));

        _grid.RowStyle = rowStyle;
    }

    private bool FilterRow(object item)
    {
        if (item is not CountRow row)
            return false;

        var query = _search.Text.Trim();

        if (!string.IsNullOrWhiteSpace(query) &&
            !row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !row.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var selected = (_filter.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "all";

        return selected switch
        {
            "shortage" => row.Difference < 0,
            "surplus" => row.Difference > 0,
            "same" => row.Difference == 0,
            _ => true
        };
    }

    private void RefreshGrid()
    {
        _view.Refresh();
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var shortage = _rows
            .Where(x => x.Difference < 0)
            .Sum(x => -x.Difference);

        var surplus = _rows
            .Where(x => x.Difference > 0)
            .Sum(x => x.Difference);

        var changedCount = _rows.Count(x => x.Difference != 0);
        var netDifference = surplus - shortage;
        var visibleCount = _view?.Cast<object>().Count() ?? _rows.Count;

        _summary.Text = _arabic
            ? $"إجمالي الأصناف: {_rows.Count:N0}  |  المعروض: {visibleCount:N0}\n" +
              $"أصناف بها فروق: {changedCount:N0}  |  " +
              $"العجز: {shortage:N3}  |  الزيادة: {surplus:N3}  |  " +
              $"صافي الفرق: {netDifference:+0.###;-0.###;0}"
            : $"Total products: {_rows.Count:N0}  |  Visible: {visibleCount:N0}\n" +
              $"Products with differences: {changedCount:N0}  |  " +
              $"Shortage: {shortage:N3}  |  Surplus: {surplus:N3}  |  " +
              $"Net difference: {netDifference:+0.###;-0.###;0}";

        _summary.Foreground = netDifference < 0 ? Red :
            netDifference > 0 ? Green : Navy;
    }

    private void SaveAdjustment(object sender, RoutedEventArgs e)
    {
        if (_isSaving)
            return;

        _isSaving = true;
        _saveButton.IsEnabled = false;

        try
        {
            _grid.CommitEdit(DataGridEditingUnit.Cell, true);
            _grid.CommitEdit(DataGridEditingUnit.Row, true);

            var session = App.Services.GetRequiredService<CurrentUserSession>();

            if (!session.UserId.HasValue)
                throw new InvalidOperationException(
                    _arabic ? "يجب تسجيل الدخول أولاً." : "You must sign in first.");

            if (string.IsNullOrWhiteSpace(_reason.Text))
                throw new InvalidOperationException(
                    _arabic ? "اكتب سبب الجرد." : "Enter a reason for the stock count.");

            if (_rows.Any(x =>
                    x.CountedQuantity < 0 ||
                    decimal.Round(x.CountedQuantity, 3) != x.CountedQuantity))
            {
                throw new InvalidOperationException(
                    _arabic
                        ? "الكميات يجب أن تكون غير سالبة وبحد أقصى 3 منازل عشرية."
                        : "Quantities must be non-negative with at most 3 decimal places.");
            }

            var changed = _rows
                .Where(x => x.CountedQuantity != x.SystemQuantity)
                .ToList();

            if (changed.Count == 0)
                throw new InvalidOperationException(
                    _arabic ? "لا توجد فروق لتسويتها." : "There are no differences to adjust.");

            using var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable);

            var currentBalances = changed.ToDictionary(
                row => row.ProductId,
                row => _db.StockBalances.SingleOrDefault(
                    x => x.ProductId == row.ProductId)
                    ?? throw new InvalidOperationException(
                        (_arabic ? "رصيد الصنف غير موجود: " : "Stock balance not found for product: ")
                        + row.Name));

            var actualChanges = changed
                .Select(row => new
                {
                    Row = row,
                    Balance = currentBalances[row.ProductId],
                    Delta = row.CountedQuantity -
                            currentBalances[row.ProductId].Quantity
                })
                .Where(x => x.Delta != 0m)
                .ToList();

            if (actualChanges.Count == 0)
            {
                throw new InvalidOperationException(
                    _arabic
                        ? "الأرصدة تغيرت ولم تعد تحتاج إلى تسوية. راجع الكميات مرة أخرى."
                        : "Balances have changed and no longer require adjustment. Review the quantities.");
            }

            var reason = _reason.Text.Trim();

            var adjustment = new StockAdjustment(reason, session.UserId.Value);

            foreach (var change in actualChanges)
            {
                adjustment.AddItem(new StockAdjustmentItem(
                    change.Row.ProductId,
                    change.Balance.Quantity,
                    change.Row.CountedQuantity));
            }

            adjustment.Approve(session.UserId.Value);
            _db.StockAdjustments.Add(adjustment);
            _db.SaveChanges();

            foreach (var change in actualChanges)
            {
                var row = change.Row;
                var balance = change.Balance;
                var delta = change.Delta;

                if (delta > 0)
                    balance.Increase(delta);
                else
                    balance.Decrease(-delta);

                _db.StockMovements.Add(new StockMovement(
                    row.ProductId,
                    delta > 0
                        ? StockMovementType.AdjustmentIncrease
                        : StockMovementType.AdjustmentDecrease,
                    delta,
                    "StockAdjustment",
                    adjustment.Id.ToString(),
                    reason,
                    session.UserId.Value));
            }

            _db.SaveChanges();
            tx.Commit();

            MessageBox.Show(
                this,
                _arabic
                    ? $"تم اعتماد الجرد وتحديث المخزون بنجاح.\nعدد الأصناف التي تمت تسويتها: {actualChanges.Count}"
                    : $"Stock count approved successfully.\nAdjusted products: {actualChanges.Count}",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            try
            {
                if (_db.Database.CurrentTransaction != null)
                    _db.Database.CurrentTransaction.Rollback();
            }
            catch
            {
                // Preserve the original exception.
            }

            _db.ChangeTracker.Clear();

            MessageBox.Show(
                this,
                (_arabic ? "تعذر اعتماد التسوية: " : "Could not approve adjustment: ")
                + ex.GetBaseException().Message,
                _arabic ? "خطأ" : "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isSaving = false;
            _saveButton.IsEnabled = true;
        }
    }

    private sealed class CountRow
    {
        public int ProductId { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public decimal SystemQuantity { get; init; }
        public decimal CountedQuantity { get; set; }
        public decimal Difference => CountedQuantity - SystemQuantity;
        public string Unit { get; init; } = "";
    }
}
