
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Views;

public sealed partial class CategoryManagementWindow : Window
{
    private readonly AppDbContext _db;
    private readonly bool _ar;

    private Category? _selected;

    public CategoryManagementWindow(AppDbContext db)
    {
        InitializeComponent();

        _db = db;

        _ar = LocalizationManager.Current.LanguageCode
            .StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        Loaded += (_, _) => LoadCategories();
    }

    private void LoadCategories(int? selectId = null)
    {
        var categories = _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryRow
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                IsActive = c.IsActive,
                ProductCount = _db.Products.Count(
                    p => p.CategoryId == c.Id)
            })
            .ToList();

        CategoriesGrid.ItemsSource = categories;

        CountTextBlock.Text = _ar
            ? $"عدد التصنيفات: {categories.Count}"
            : $"Categories: {categories.Count}";

        if (selectId.HasValue)
        {
            CategoriesGrid.SelectedItem = categories
                .FirstOrDefault(c => c.Id == selectId.Value);
        }
    }

    private void CategoriesGrid_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (CategoriesGrid.SelectedItem is not CategoryRow row)
            return;

        SelectCategory(row.Id);
    }

    private void SelectCategory(int id)
    {
        _selected = _db.Categories
            .SingleOrDefault(c => c.Id == id);

        if (_selected is null)
            return;

        NameTextBox.Text = _selected.Name;
        CodeTextBox.Text = _selected.Code ?? string.Empty;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCategory(update: false);
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCategory(update: true);
    }

    private void SaveCategory(bool update)
    {
        try
        {
            string name = NameTextBox.Text.Trim();

            string? code = string.IsNullOrWhiteSpace(CodeTextBox.Text)
                ? null
                : CodeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                Warn(_ar
                    ? "اكتب اسم التصنيف."
                    : "Enter a category name.");
                return;
            }

            if (update && _selected is null)
            {
                Warn(_ar
                    ? "حدد تصنيفًا من الجدول أولًا."
                    : "Select a category first.");
                return;
            }

            bool duplicateName = _db.Categories.Any(c =>
                c.Name == name &&
                (!update || c.Id != _selected!.Id));

            if (duplicateName)
            {
                Warn(_ar
                    ? "اسم التصنيف مستخدم بالفعل."
                    : "Category name already exists.");
                return;
            }

            bool duplicateCode = code is not null &&
                _db.Categories.Any(c =>
                    c.Code == code &&
                    (!update || c.Id != _selected!.Id));

            if (duplicateCode)
            {
                Warn(_ar
                    ? "كود التصنيف مستخدم بالفعل."
                    : "Category code already exists.");
                return;
            }

            int id;

            if (update)
            {
                _selected!.UpdateDetails(name, code);
                id = _selected.Id;
            }
            else
            {
                var category = new Category(name, code);

                _db.Categories.Add(category);
                _db.SaveChanges();

                id = category.Id;

                LoadCategories(id);
                ClearFields();

                Warn(_ar
                    ? "تمت إضافة التصنيف بنجاح."
                    : "Category added successfully.",
                    MessageBoxImage.Information);

                return;
            }

            _db.SaveChanges();

            LoadCategories(id);

            Warn(_ar
                ? "تم حفظ التعديلات بنجاح."
                : "Changes saved successfully.",
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Warn(
                (_ar ? "تعذر حفظ التصنيف: " : "Could not save category: ")
                + ex.GetBaseException().Message);
        }
    }

    private void EditRowButton_Click(
        object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not CategoryRow row)
            return;

        CategoriesGrid.SelectedItem = row;
        SelectCategory(row.Id);
        NameTextBox.Focus();
    }

    private void ToggleRowButton_Click(
        object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not CategoryRow row)
            return;

        SelectCategory(row.Id);

        if (_selected is null)
            return;

        string action = _selected.IsActive
            ? (_ar ? "تعطيل" : "deactivate")
            : (_ar ? "تفعيل" : "activate");

        var result = MessageBox.Show(
            this,
            _ar
                ? $"هل تريد {action} التصنيف «{_selected.Name}»؟"
                : $"Do you want to {action} category '{_selected.Name}'?",
            Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            if (_selected.IsActive)
                _selected.Deactivate();
            else
                _selected.Activate();

            int id = _selected.Id;

            _db.SaveChanges();
            LoadCategories(id);
        }
        catch (Exception ex)
        {
            Warn(
                (_ar
                    ? "تعذر تغيير حالة التصنيف: "
                    : "Could not change category status: ")
                + ex.GetBaseException().Message);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearFields();
    }

    private void ClearFields()
    {
        _selected = null;
        CategoriesGrid.SelectedItem = null;
        NameTextBox.Clear();
        CodeTextBox.Clear();
    }

    private void Warn(
        string message,
        MessageBoxImage image = MessageBoxImage.Warning)
    {
        MessageBox.Show(
            this,
            message,
            Title,
            MessageBoxButton.OK,
            image);
    }

    private sealed class CategoryRow
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Code { get; init; }

        public bool IsActive { get; init; }

        public int ProductCount { get; init; }
    }
}
