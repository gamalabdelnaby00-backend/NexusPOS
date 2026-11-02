using System;
using System.Linq;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Views;

/// <summary>Product creation dialog backed by the existing product/category/stock tables.</summary>
public sealed partial class ProductEditorWindow : Window
{
    private static readonly Color Navy = Color.FromRgb(20, 43, 72);
    private static readonly Color Blue = Color.FromRgb(22, 105, 232);
    private static readonly Color Border = Color.FromRgb(215, 224, 237);
    private readonly AppDbContext _db;
    private readonly Product? _editingProduct;
    private readonly bool _ar;
    private readonly TextBox _name;
    private readonly TextBox _code;
    private readonly TextBox _barcode;
    private readonly TextBox _price;
    private readonly TextBox _cost;
    private readonly TextBox _minimum;
    private readonly TextBox _opening;
    private readonly ComboBox _category;
    private readonly ComboBox _unit;
    private readonly TextBlock _imageName;
    private string? _selectedImagePath;

    public Product? CreatedProduct { get; private set; }
    public decimal OpeningQuantity { get; private set; }
    public string? SelectedImagePath => _selectedImagePath;

    public ProductEditorWindow(AppDbContext db, Product? productToEdit = null)
    {
        _db = db;
        _editingProduct = productToEdit;
        _ar = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        InitializeComponent();
        Title = productToEdit is null ? (_ar ? "إضافة منتج جديد" : "Add new product") : (_ar ? "تعديل بيانات المنتج" : "Edit product");
        FlowDirection = _ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        _name = NameTextBox; _code = CodeTextBox; _barcode = BarcodeTextBox;
        _price = PriceTextBox; _cost = CostTextBox; _minimum = MinimumTextBox;
        _opening = OpeningTextBox; _category = CategoryComboBox; _unit = UnitComboBox; _imageName = ImageNameText;
        HeaderTitle.Text = _editingProduct is null ? (_ar ? "إضافة منتج جديد" : "Add new product") : (_ar ? "تعديل بيانات المنتج" : "Edit product");
        HeaderSubtitle.Text = _editingProduct is null ? (_ar ? "أدخل بيانات المنتج الأساسية والمخزون الافتتاحي" : "Enter product details and opening stock") : (_ar ? "عدّل بيانات المنتج دون تغيير رصيد المخزون الحالي" : "Update product details without changing current stock");
        NameLabel.Text = _ar ? "اسم المنتج *" : "Product name *";
        CodeLabel.Text = _ar ? "كود المنتج *" : "Product code *";
        BarcodeLabel.Text = _ar ? "الباركود *" : "Barcode *";
        CategoryLabel.Text = _ar ? "التصنيف *" : "Category *";
        SellingPriceLabel.Text = _ar ? "سعر البيع *" : "Selling price *";
        CostPriceLabel.Text = _ar ? "سعر التكلفة" : "Cost price";
        MinimumLabel.Text = _ar ? "حد إعادة الطلب" : "Reorder threshold";
        UnitLabelText.Text = _ar ? "وحدة القياس" : "Unit of measure";
        ImageLabel.Text = _ar ? "صورة المنتج (اختياري)" : "Product image (optional)";
        ChooseImageButton.Content = _ar ? "اختيار صورة" : "Choose image";
        AddCategoryButton.Content = _ar ? "+ تصنيف" : "+ Category";
        OpeningLabel.Text = _ar ? "الكمية الافتتاحية" : "Opening quantity";
        CancelButton.Content = _ar ? "إلغاء" : "Cancel";
        SaveButton.Content = _editingProduct is null ? (_ar ? "حفظ المنتج" : "Save product") : (_ar ? "حفظ التعديلات" : "Save changes");
        OpeningPanel.Visibility = _editingProduct is null ? Visibility.Visible : Visibility.Collapsed;
        ExistingStockText.Visibility = _editingProduct is null ? Visibility.Collapsed : Visibility.Visible;
        _unit.ItemsSource = Enum.GetValues<UnitOfMeasure>().Select(x => new UnitItem(x, UnitLabel(x))).ToList();
        _unit.DisplayMemberPath = nameof(UnitItem.Label);
        _unit.SelectedValuePath = nameof(UnitItem.Value);
        _unit.SelectedValue = UnitOfMeasure.Piece;
        if (_editingProduct is not null)
        {
            ChooseImageButton.IsEnabled = false;
            ChooseImageButton.ToolTip = _ar ? "تغيير الصورة غير مشمول في هذه الخطوة؛ تم الحفاظ على الصورة الحالية." : "Image replacement is not included in this step; the current image is preserved.";
        }
        LoadCategories(productToEdit?.CategoryId);
        if (productToEdit is not null) PopulateForEdit(productToEdit);
    }

    private void PopulateForEdit(Product product)
    {
        _name.Text = product.Name;
        _code.Text = product.Code;
        _barcode.Text = product.Barcode;
        _price.Text = product.Price.ToString("0.00");
        _cost.Text = product.CostPrice.ToString("0.00");
        _minimum.Text = product.MinStock.ToString("0.###");
        _unit.SelectedValue = product.UnitOfMeasure;
        _opening.IsEnabled = false;
        _imageName.Text = string.IsNullOrWhiteSpace(product.ImageKey) ? (_ar ? "لا توجد صورة محفوظة" : "No saved image") : Path.GetFileName(product.ImageKey);
    }

    private void LoadCategories(int? selectId = null)
    {
        try
        {
            var categories = _db.Categories.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Name).Select(x => new CategoryOption(x.Id, x.Name)).ToList();
            _category.ItemsSource = categories;
            _category.DisplayMemberPath = nameof(CategoryOption.Name);
            _category.SelectedValuePath = nameof(CategoryOption.Id);
            _category.IsEnabled = categories.Count > 0;
            if (selectId.HasValue && categories.Any(x => x.Id == selectId.Value)) _category.SelectedValue = selectId.Value;
            else if (categories.Count > 0 && _category.SelectedIndex < 0) _category.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _category.ItemsSource = null;
            _category.IsEnabled = false;
            Warn(_ar ? "تعذر تحميل التصنيفات من قاعدة البيانات: " + ex.GetBaseException().Message : "Could not load categories from the database: " + ex.GetBaseException().Message);
        }
    }

    private void AddCategory_Click(object? sender, RoutedEventArgs e)
    {
        var prompt = new Window { Owner = this, Title = _ar ? "إضافة تصنيف" : "Add category", Width = 380, Height = 210, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White, FlowDirection = FlowDirection };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = _ar ? "اسم التصنيف" : "Category name", Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.SemiBold });
        var name = Field(); panel.Children.Add(name);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = _ar ? "إلغاء" : "Cancel", MinWidth = 85, Height = 34, Margin = new Thickness(0, 0, 8, 0) }; cancel.Click += (_, _) => prompt.DialogResult = false;
        var save = new Button { Content = _ar ? "حفظ" : "Save", MinWidth = 85, Height = 34, Background = new SolidColorBrush(Blue), Foreground = Brushes.White };
        save.Click += (_, _) => { if (string.IsNullOrWhiteSpace(name.Text)) { Warn(_ar ? "اكتب اسم التصنيف." : "Enter a category name."); return; } prompt.DialogResult = true; };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons); prompt.Content = panel;
        if (prompt.ShowDialog() != true) return;
        try
        {
            var categoryName = name.Text.Trim();
            if (_db.Categories.Any(c => c.Name == categoryName)) { Warn(_ar ? "التصنيف موجود بالفعل." : "Category already exists."); return; }
            var category = new Category(categoryName);
            _db.Categories.Add(category); _db.SaveChanges();
            LoadCategories(category.Id);
        }
        catch (Exception ex) { Warn(_ar ? "تعذر حفظ التصنيف: " + ex.GetBaseException().Message : "Could not save category: " + ex.GetBaseException().Message); }
    }

    private void ChooseImage_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = _ar ? "اختيار صورة المنتج" : "Choose product image", Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        var info = new FileInfo(dialog.FileName);
        if (info.Length > 5 * 1024 * 1024) { Warn(_ar ? "حجم الصورة يجب ألا يتجاوز 5 ميجابايت." : "Image must be 5 MB or smaller."); return; }
        if (info.Length == 0) { Warn(_ar ? "ملف الصورة فارغ." : "Image file is empty."); return; }
        _selectedImagePath = dialog.FileName;
        _imageName.Text = info.Name;
        _imageName.ToolTip = dialog.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_code.Text) || string.IsNullOrWhiteSpace(_barcode.Text))
        {
            Warn(_ar ? "أدخل اسم المنتج والكود والباركود." : "Enter product name, code and barcode."); return;
        }
        if (_category.SelectedValue is null || !int.TryParse(_category.SelectedValue.ToString(), out _))
        {
            Warn(_ar ? "لا يوجد تصنيف محدد. اختر تصنيفًا من القائمة أو اضغط + تصنيف لإضافته." : "No category selected. Choose one or use + Category to add it."); return;
        }
        if (!decimal.TryParse(_price.Text, out var price) || price < 0 ||
            !decimal.TryParse(_cost.Text, out var cost) || cost < 0 ||
            !decimal.TryParse(_minimum.Text, out var min) || min < 0 ||
            !decimal.TryParse(_opening.Text, out var opening) || opening < 0 || decimal.Round(opening, 3) != opening)
        {
            Warn(_ar ? "راجع الأسعار وحد إعادة الطلب والكمية؛ يجب أن تكون أرقامًا صحيحة وغير سالبة، والكمية حتى 3 منازل عشرية." : "Check prices, reorder threshold and quantity. Values must be valid and non-negative; quantity supports up to 3 decimal places."); return;
        }
        var code = _code.Text.Trim(); var barcode = _barcode.Text.Trim();
        // Capture the current ID outside the EF expression tree; null propagation is not supported in expression trees.
        var editingProductId = _editingProduct is null ? 0 : _editingProduct.Id;
        if (_db.Products.Any(x => x.Id != editingProductId && x.Code == code)) { Warn(_ar ? "كود المنتج مستخدم بالفعل." : "Product code already exists."); return; }
        if (_db.Products.Any(x => x.Id != editingProductId && x.Barcode == barcode)) { Warn(_ar ? "الباركود مستخدم بالفعل." : "Barcode already exists."); return; }
        try
        {
            var categoryId = Convert.ToInt32(_category.SelectedValue);
            var unit = _unit.SelectedValue is UnitOfMeasure selectedUnit ? selectedUnit : UnitOfMeasure.Piece;
            if (_editingProduct is null)
            {
                CreatedProduct = new Product(_name.Text.Trim(), price, barcode, categoryId, unit, code, cost, min);
                OpeningQuantity = opening;
            }
            else
            {
                _editingProduct.ChangeName(_name.Text.Trim());
                _editingProduct.ChangeCode(code);
                _editingProduct.ChangeBarcode(barcode);
                _editingProduct.ChangeCategory(categoryId);
                _editingProduct.ChangeUnitOfMeasure(unit);
                _editingProduct.ChangePrice(price);
                _editingProduct.ChangeCost(cost);
                _editingProduct.ChangeMinStock(min);
                CreatedProduct = _editingProduct;
            }
            DialogResult = true;
        }
        catch (Exception ex) { Warn(ex.GetBaseException().Message); }
    }

    private void Warn(string message) => MessageBox.Show(this, message, _ar ? "راجع البيانات" : "Check input", MessageBoxButton.OK, MessageBoxImage.Warning);
    private string UnitLabel(UnitOfMeasure unit) => _ar ? unit switch
    {
        UnitOfMeasure.Piece => "قطعة", UnitOfMeasure.Kilogram => "كيلوجرام", UnitOfMeasure.Gram => "جرام",
        UnitOfMeasure.Liter => "لتر", UnitOfMeasure.Milliliter => "ملليلتر", UnitOfMeasure.Meter => "متر", _ => unit.ToString()
    } : unit.ToString();
    private static TextBox Field() => new() { Height = 40, Padding = new Thickness(10, 5, 10, 5), VerticalContentAlignment = VerticalAlignment.Center, BorderBrush = new SolidColorBrush(Border), BorderThickness = new Thickness(1), Background = Brushes.White, FontSize = 13 };
    private sealed record UnitItem(UnitOfMeasure Value, string Label);
    private sealed record CategoryOption(int Id, string Name);
}
