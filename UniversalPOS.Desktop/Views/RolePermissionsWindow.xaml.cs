using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UniversalPOS.Infrastructure.Security;

namespace UniversalPOS.Desktop.Views;

/// <summary>Role permission editor. Changes are staged in the UI and saved through the guarded service.</summary>
public sealed partial class RolePermissionsWindow : Window
{
    private sealed record PermissionCheck(RolePermissionItem Permission, CheckBox CheckBox);
    private static readonly Color Navy = Color.FromRgb(20, 45, 75);
    private static readonly Color Blue = Color.FromRgb(17, 105, 232);
    private static readonly Color Canvas = Color.FromRgb(244, 248, 253);
    private readonly RolePermissionManagementService _service;
    private readonly bool _ar = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    private readonly ComboBox _roles = new() { MinWidth = 260, Height = 38, DisplayMemberPath = "Name", Margin = new Thickness(0, 6, 0, 0) };
    private readonly StackPanel _groups = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(95, 112, 132)) };
    private readonly Button _save = new() { Content = "حفظ التعديلات", Padding = new Thickness(18, 9, 18, 9), MinHeight = 38, Margin = new Thickness(0, 12, 8, 0), FontWeight = FontWeights.SemiBold };
    private readonly Button _reload = new() { Content = "إعادة تحميل", Padding = new Thickness(18, 9, 18, 9), MinHeight = 38, Margin = new Thickness(0, 12, 0, 0) };
    private readonly List<PermissionCheck> _checks = new();
    private IReadOnlyList<RolePermissionGroup> _loadedGroups = Array.Empty<RolePermissionGroup>();

    public RolePermissionsWindow(RolePermissionManagementService service)
    {
        _service = service;
        Title = T("إدارة الأدوار والصلاحيات | NexusPOS", "Role & Permission Management | NexusPOS");
        FlowDirection = LocalizationManager.Current.FlowDirection;
        Width = 1000; Height = 760; MinWidth = 760; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Canvas); FontFamily = new FontFamily("Segoe UI");
        _roles.Padding = new Thickness(10, 5, 10, 5); _roles.Background = Brushes.White; _roles.BorderBrush = Brush(Color.FromRgb(220, 230, 242));
        _roles.SelectionChanged += async (_, _) => { if (_roles.SelectedItem is ManageableRoleOption) await LoadPermissionsAsync(); };
        _save.Content = T("حفظ التعديلات", "Save changes"); _reload.Content = T("إعادة تحميل", "Reload");
        StyleButton(_save, true); StyleButton(_reload, false);
        _save.Click += async (_, _) => await SaveAsync(); _reload.Click += async (_, _) => await LoadPermissionsAsync();

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock { Text = T("إدارة الأدوار والصلاحيات", "Role & Permission Management"), FontSize = 25, FontWeight = FontWeights.Bold, Foreground = Brush(Navy) });
        heading.Children.Add(new TextBlock { Text = T("اختر الدور ثم حدد الصلاحيات المطلوبة. تُحفظ التغييرات بعد التحقق من صلاحياتك.", "Select a role, choose its permissions, then save after authorization checks."), FontSize = 13, Foreground = Brush(Color.FromRgb(105, 122, 143)), Margin = new Thickness(0, 5, 0, 0) });
        var rolePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 14), MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        rolePanel.Children.Add(new TextBlock { Text = T("الدور المستهدف", "Target role"), Foreground = Brush(Navy), FontWeight = FontWeights.SemiBold }); rolePanel.Children.Add(_roles);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _groups };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        actions.Children.Add(_save); actions.Children.Add(_reload);
        var body = new DockPanel(); DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading); DockPanel.SetDock(rolePanel, Dock.Top); body.Children.Add(rolePanel); DockPanel.SetDock(actions, Dock.Bottom); body.Children.Add(actions); DockPanel.SetDock(_status, Dock.Bottom); body.Children.Add(_status); body.Children.Add(scroll);
        Content = new Border { Background = Brushes.White, BorderBrush = Brush(Color.FromRgb(226, 234, 244)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(22), Margin = new Thickness(22), Child = body };
        Loaded += async (_, _) => await LoadRolesAsync();
    }

    private string T(string ar, string en) => _ar ? ar : en;
    private static SolidColorBrush Brush(Color c) => new(c);
    private static void StyleButton(Button b, bool primary) { b.Background = Brush(primary ? Blue : Colors.White); b.Foreground = Brush(primary ? Colors.White : Navy); b.BorderBrush = Brush(primary ? Blue : Color.FromRgb(218, 229, 242)); b.BorderThickness = new Thickness(1); b.Cursor = System.Windows.Input.Cursors.Hand; }

    private async Task LoadRolesAsync()
    {
        try
        {
            _roles.ItemsSource = await _service.GetManageableRolesAsync();
            if (_roles.Items.Count > 0) _roles.SelectedIndex = 0;
            else { _groups.Children.Clear(); _status.Text = T("لا توجد أدوار يمكنك إدارتها وفق صلاحياتك الحالية.", "No roles are available to manage with your current permissions."); }
        }
        catch (Exception ex) { _status.Text = ErrorMessageMapper.ToUserMessage(ex); _save.IsEnabled = false; }
    }

    private async Task LoadPermissionsAsync()
    {
        if (_roles.SelectedItem is not ManageableRoleOption role) return;
        try
        {
            _loadedGroups = await _service.GetRolePermissionsAsync(role.Id);
            _checks.Clear(); _groups.Children.Clear();
            foreach (var group in _loadedGroups)
            {
                var panel = new StackPanel { Margin = new Thickness(4, 4, 4, 8) };
                foreach (var permission in group.Permissions)
                {
                    var check = new CheckBox { Content = $"{permission.Name}  ({permission.Code})", IsChecked = permission.IsGranted, Tag = permission.PermissionId, Margin = new Thickness(8, 7, 8, 7), Foreground = Brush(Navy), FontSize = 13, ToolTip = permission.Description };
                    panel.Children.Add(check); _checks.Add(new PermissionCheck(permission, check));
                }
                var expander = new Expander { Header = group.Module, IsExpanded = true, Content = panel, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12), Background = Brushes.White, BorderBrush = Brush(Color.FromRgb(226, 234, 244)), BorderThickness = new Thickness(1), Foreground = Brush(Navy), FontWeight = FontWeights.SemiBold };
                _groups.Children.Add(expander);
            }
            _save.IsEnabled = true; _status.Text = T($"الدور: {role.Name} — عدد الصلاحيات: {_checks.Count}", $"Role: {role.Name} — permissions: {_checks.Count}");
        }
        catch (Exception ex) { _status.Text = ErrorMessageMapper.ToUserMessage(ex); _save.IsEnabled = false; }
    }

    private async Task SaveAsync()
    {
        if (_roles.SelectedItem is not ManageableRoleOption role) return;
        _save.IsEnabled = false;
        try
        {
            int changed = 0;
            foreach (var item in _checks)
            {
                bool requested = item.CheckBox.IsChecked == true;
                if (requested == item.Permission.IsGranted) continue;
                await _service.SetPermissionAsync(role.Id, item.Permission.PermissionId, requested);
                changed++;
            }
            await LoadPermissionsAsync();
            _status.Text = T($"تم حفظ التغييرات. التعديلات: {changed}.", $"Changes saved. Updated permissions: {changed}.");
        }
        catch (Exception ex)
        {
            _status.Text = ErrorMessageMapper.ToUserMessage(ex);
            await LoadPermissionsAsync();
        }
        finally { _save.IsEnabled = true; }
    }
}
