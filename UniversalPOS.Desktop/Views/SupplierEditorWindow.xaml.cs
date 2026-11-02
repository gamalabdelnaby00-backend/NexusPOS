
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Views;

public sealed partial class SupplierEditorWindow : Window
{
    private static readonly Color Navy = Color.FromRgb(20, 43, 72);
    private static readonly Color Blue = Color.FromRgb(22, 105, 232);
    private static readonly Color BorderColor = Color.FromRgb(215, 224, 237);

    private readonly AppDbContext _db;
    private readonly bool _ar;

    private readonly TextBox _name = CreateField();
    private readonly TextBox _phone = CreateField();
    private readonly TextBox _taxNumber = CreateField();

    public Supplier? CreatedSupplier { get; private set; }

    public SupplierEditorWindow(AppDbContext db)
    {
        _db = db;

        _ar = LocalizationManager.Current.LanguageCode
            .StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        Title = _ar ? "إضافة مورد جديد" : "Add New Supplier";
        Width = 620;
        Height = 490;
        MinWidth = 540;
        MinHeight = 440;

        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(244, 247, 252));
        FontFamily = new FontFamily("Segoe UI");
        FlowDirection = _ar
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel();

        // Header
        var header = new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(26, 22, 26, 22),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var heading = new StackPanel();

        heading.Children.Add(new TextBlock
        {
            Text = _ar ? "إضافة مورد جديد" : "Add New Supplier",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Navy)
        });

        heading.Children.Add(new TextBlock
        {
            Text = _ar
                ? "أدخل بيانات التواصل الأساسية للمورد"
                : "Enter the supplier's contact details",
            FontSize = 13,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 7, 0, 0)
        });

        header.Child = heading;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Form
        var form = new StackPanel
        {
            Margin = new Thickness(26, 26, 26, 18)
        };

        AddField(form, _ar ? "اسم المورد *" : "Supplier Name *", _name);
        AddField(form, _ar ? "رقم الهاتف" : "Phone Number", _phone);
        AddField(form, _ar ? "الرقم الضريبي" : "Tax Number", _taxNumber);

        root.Children.Add(form);

        // Footer
        var footer = new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(26, 15, 26, 15),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = _ar ? "إلغاء" : "Cancel",
            MinWidth = 100,
            Height = 42,
            Margin = new Thickness(0, 0, 10, 0),
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Navy),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        cancelButton.Click += (_, _) => DialogResult = false;

        var saveButton = new Button
        {
            Content = _ar ? "حفظ المورد" : "Save Supplier",
            MinWidth = 135,
            Height = 42,
            Padding = new Thickness(16, 0, 16, 0),
            Background = new SolidColorBrush(Blue),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        saveButton.Click += Save_Click;

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(saveButton);

        footer.Child = buttons;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        return root;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var name = _name.Text.Trim();

        var phone = string.IsNullOrWhiteSpace(_phone.Text)
            ? null
            : _phone.Text.Trim();

        var tax = string.IsNullOrWhiteSpace(_taxNumber.Text)
            ? null
            : _taxNumber.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            Warn(_ar
                ? "اسم المورد مطلوب."
                : "Supplier name is required.");

            _name.Focus();
            return;
        }

        if (name.Length > 200 ||
            (phone?.Length ?? 0) > 50 ||
            (tax?.Length ?? 0) > 100)
        {
            Warn(_ar
                ? "تأكد من أطوال البيانات المدخلة."
                : "One or more fields exceed the allowed length.");

            return;
        }

        try
        {
            if (tax != null &&
                _db.Suppliers.AsNoTracking()
                    .Any(x => x.TaxNumber == tax))
            {
                Warn(_ar
                    ? "الرقم الضريبي مسجل بالفعل."
                    : "This tax number is already registered.");

                _taxNumber.Focus();
                return;
            }

            CreatedSupplier = new Supplier(name, phone, tax);

            // Return the created entity to the calling window.
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Warn(ex.GetBaseException().Message);
        }
    }

    private void Warn(string message)
    {
        MessageBox.Show(
            this,
            message,
            _ar ? "مراجعة البيانات" : "Check Input",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static TextBox CreateField()
    {
        return new TextBox
        {
            Height = 44,
            Padding = new Thickness(12, 6, 12, 6),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            FontSize = 14
        };
    }

    private static void AddField(
        Panel panel,
        string label,
        Control control)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 18)
        };

        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Navy),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        stack.Children.Add(control);
        panel.Children.Add(stack);
    }
}
