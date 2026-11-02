using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Employees;
using UniversalPOS.Application.Security;
using UniversalPOS.Desktop.Pages;
using UniversalPOS.Desktop.UI;
using UniversalPOS.Desktop.Views;
using UniversalPOS.Infrastructure.Data;
using UniversalPOS.Infrastructure.Security;

namespace UniversalPOS.Desktop
{
    /// <summary>
    /// Application shell: a fixed sidebar and header, with the screens swapping inside the content area.
    /// Cashier-facing flows (sales, returns) and the role / account editors keep opening as dialogs on top of it.
    /// </summary>
    public partial class MainWindow : Window
    {
        private sealed record NavItem(string Key, string Icon, string Arabic, string English, bool Extra = false);

        private static readonly NavItem[] NavItems =
        {
            new("Home", "Home", "الرئيسية", "Home"),
            new("Employees", "People", "الموظفين", "Employees"),
            new("Products", "Box", "المنتجات", "Products"),
            new("Purchases", "Cart", "المشتريات", "Purchases"),
            new("Sales", "Receipt", "المبيعات", "Sales"),
            new("Reports", "Chart", "التقارير", "Reports"),
            new("Settings", "Settings", "الإعدادات", "Settings"),
            new("Suppliers", "Truck", "الموردين", "Suppliers", true),
            new("Inventory", "Inventory", "المخزون والجرد", "Inventory & stocktaking", true),
            new("StockMovements", "Swap", "حركات المخزون", "Stock movements", true),
            new("Shifts", "Clock", "الورديات والعهدة", "Shifts & cash", true),
            new("Returns", "Return", "المرتجعات", "Returns", true),
            new("Roles", "Shield", "الأدوار والصلاحيات", "Roles & permissions", true),
            new("Accounts", "Account", "حسابات الدخول", "Login accounts", true),
        };

        private readonly CurrentUserSession _session;
        private readonly PermissionChecker _permissionChecker;
        private readonly Dictionary<string, Button> _navButtons = new();
        private IServiceScope? _pageScope;
        private string _currentKey = "Home";
        private bool _ready;

        public MainWindow()
        {
            InitializeComponent();
            _session = App.Services.GetRequiredService<CurrentUserSession>();
            _permissionChecker = App.Services.GetRequiredService<PermissionChecker>();
            Loaded += MainWindow_Loaded;
            Closed += (_, _) => { _pageScope?.Dispose(); _pageScope = null; };
        }

        private bool IsAdmin => _session.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        private bool IsCashier => _session.Roles.Contains("Cashier", StringComparer.OrdinalIgnoreCase);
        private static string T(string arabic, string english) => UiKit.T(arabic, english);

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_session.IsAuthenticated)
            {
                MessageBox.Show(T("يجب تسجيل الدخول أولاً.", "Please sign in first."), T("تسجيل الدخول", "Sign in"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            _ready = true;
            BuildChrome();
            Navigate("Home");
        }

        /// <summary>Called by AppearanceManager when the language changes. The new shell is light-only, so the theme is ignored.</summary>
        public void ApplyGlobalPreferences(string language, string theme)
        {
            if (!_ready) return;
            BuildChrome();
            ShowPage(_currentKey);
        }

        private bool Can(params string[] codes)
        {
            if (!_session.IsAuthenticated) return false;
            if (IsAdmin) return true;
            return codes.Any(code => _permissionChecker.HasPermission(code));
        }

        private bool Allowed(string key) => key switch
        {
            "Home" or "Settings" => true,
            "Employees" => Can(PermissionCodes.Employees.View, PermissionCodes.Users.View),
            // Cashiers only see the low-stock list under "Products", as before.
            "Products" => IsCashier || Can("Products.View", "Products.Create", "Products.Update"),
            "Purchases" or "Suppliers" => Can("Purchases.View", "Purchases.Create", "Suppliers.View", "SupplierPayments.View"),
            // Current approved cashier policy: sales and returns are always available to Cashier accounts.
            "Sales" => IsCashier || Can("Sales.View", "Sales.Create", "Sales.CreateSale"),
            "Returns" => IsCashier || Can("Returns.View", "Returns.Create", "Returns.Process"),
            "Reports" => Can("Reports.View", "Reports.Sales", "Reports.Inventory"),
            "Inventory" or "StockMovements" => Can("Inventory.View", "Inventory.Adjust", "Inventory.Manage"),
            "Shifts" => Can("Shifts.View", "Shifts.Open", "Shifts.Close", "CashShifts.View", "CashShifts.Open"),
            "Roles" => Can("Roles.View", "Roles.Manage", "Permissions.View"),
            "Accounts" => Can("Users.View", "Users.Create", "Users.Manage"),
            _ => false
        };

        // ------------------------------------------------------------------ chrome (sidebar + header)

        private void BuildChrome()
        {
            var direction = LocalizationManager.Current.FlowDirection;
            ContentRoot.FlowDirection = direction;
            SidebarBorder.FlowDirection = direction;
            Title = "NexusPOS";
            BuildBrand();
            BuildSidebar();
            BuildHeader();
        }

        private void BuildBrand()
        {
            BrandPanel.Children.Clear();
            var logo = new Border
            {
                Width = 38, Height = 38, CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(UiKit.B("#1A8CFF").Color, UiKit.B("#0A5FD6").Color, 45),
                Child = new TextBlock { Text = "N", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            BrandPanel.Children.Add(logo);
            BrandPanel.Children.Add(new TextBlock
            {
                Text = "NexusPOS", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold,
                Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            });
        }

        private void BuildSidebar()
        {
            NavPanel.Children.Clear();
            SidebarFooter.Children.Clear();
            _navButtons.Clear();

            bool extrasHeaderAdded = false;
            foreach (var item in NavItems)
            {
                if (!Allowed(item.Key)) continue;
                if (item.Extra && !extrasHeaderAdded)
                {
                    extrasHeaderAdded = true;
                    NavPanel.Children.Add(new Border { Height = 1, Background = UiKit.B("#1D4575"), Margin = new Thickness(6, 12, 6, 8) });
                    NavPanel.Children.Add(new TextBlock
                    {
                        Text = T("أقسام إضافية", "More sections"), Foreground = UiKit.B("#8FB0D8"), FontSize = 11,
                        Margin = new Thickness(10, 0, 0, 6)
                    });
                }
                var button = NavButton(item.Icon, T(item.Arabic, item.English));
                var key = item.Key;
                button.Click += (_, _) => Navigate(key);
                _navButtons[item.Key] = button;
                NavPanel.Children.Add(button);
            }

            var logout = NavButton("Logout", T("تسجيل الخروج", "Sign out"), UiKit.B("#F4B6B6"));
            logout.Click += (_, _) => Logout();
            SidebarFooter.Children.Add(new Border { Height = 1, Background = UiKit.B("#1D4575"), Margin = new Thickness(6, 0, 6, 8) });
            SidebarFooter.Children.Add(logout);
            HighlightNav();
        }

        private static Button NavButton(string icon, string text, Brush? foreground = null)
        {
            var button = new Button
            {
                Style = (Style)System.Windows.Application.Current.FindResource("ShellNavButton"),
                Height = 42, Margin = new Thickness(0, 1, 0, 1)
            };
            if (foreground is not null) button.Foreground = foreground;
            var iconPath = UiKit.Icon(icon, 20, Brushes.White);
            // The icon follows the button's foreground, so the selected / hover colours apply to it too.
            iconPath.SetBinding(Shape.FillProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) });
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(iconPath);
            panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            button.Content = panel;
            return button;
        }

        private void HighlightNav()
        {
            foreach (var (key, button) in _navButtons)
                button.Tag = key == _currentKey ? "Selected" : null;
        }

        private void BuildHeader()
        {
            var grid = new Grid();
            var user = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };

            var avatar = new Border
            {
                Width = 42, Height = 42, CornerRadius = new CornerRadius(21), Background = UiKit.B("#0A74F0"),
                Child = new Viewbox { Margin = new Thickness(8), Child = UiKit.Icon("Account", 26, Brushes.White) }
            };
            var texts = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(new TextBlock { Text = _session.DisplayName ?? string.Empty, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = UiKit.Ink });
            texts.Children.Add(new TextBlock { Text = string.Join(" / ", _session.Roles), FontSize = 12, Foreground = UiKit.Muted, Margin = new Thickness(0, 2, 0, 0) });

            var bell = new Button
            {
                Style = (Style)System.Windows.Application.Current.FindResource("ShellButton"), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
                Width = 40, Height = 40, Padding = new Thickness(0), Margin = new Thickness(22, 0, 0, 0),
                ToolTip = T("الأصناف منخفضة المخزون", "Low-stock items"), Content = UiKit.Icon("Bell", 22, UiKit.B("#0A74F0"))
            };
            bell.Click += (_, _) => { if (Allowed("Inventory") || IsCashier) ShowSection("LowStock", T("الأصناف منخفضة المخزون", "Low-stock products")); };

            user.Children.Add(avatar); user.Children.Add(texts); user.Children.Add(bell);
            grid.Children.Add(user);

            // Language switch, same choices as the sign-in screen; the preference is saved and shared by both.
            var language = BuildLanguageSwitch();
            language.HorizontalAlignment = HorizontalAlignment.Right;
            language.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(language);

            HeaderBorder.Child = grid;
        }

        private FrameworkElement BuildLanguageSwitch()
        {
            bool arabic = UiKit.IsArabic;
            var host = new Grid();

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(UiKit.Icon("Globe", 20, UiKit.B("#0B5FCC")));
            content.Children.Add(new TextBlock
            {
                Text = arabic ? "العربية" : "English", Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold, Foreground = UiKit.Ink
            });
            content.Children.Add(UiKit.Icon("ChevronDown", 16, UiKit.Muted));

            var button = new Button
            {
                Style = (Style)System.Windows.Application.Current.FindResource("ShellButton"), Background = Brushes.White, BorderBrush = UiKit.B("#D3E2F5"),
                Foreground = UiKit.Ink, Height = 40, Padding = new Thickness(12, 0, 12, 0),
                ToolTip = T("تغيير اللغة", "Change language"), Content = content
            };

            var popup = new Popup
            {
                PlacementTarget = button, Placement = PlacementMode.Bottom, StaysOpen = false,
                AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade
            };
            var menu = new StackPanel { FlowDirection = LocalizationManager.Current.FlowDirection };
            menu.Children.Add(LanguageOption("العربية", "ar", arabic, popup));
            menu.Children.Add(LanguageOption("English", "en", !arabic, popup));
            popup.Child = new Border
            {
                MinWidth = 180, Margin = new Thickness(8, 6, 8, 14), Background = Brushes.White, BorderBrush = UiKit.B("#D3E2F5"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Child = menu,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = 0.16, Color = Color.FromRgb(0x0B, 0x2A, 0x4F) }
            };

            button.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
            host.Children.Add(button);
            host.Children.Add(popup);
            return host;
        }

        private static Button LanguageOption(string text, string code, bool selected, Popup popup)
        {
            var row = new Grid { MinWidth = 150 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Foreground = UiKit.Ink, VerticalAlignment = VerticalAlignment.Center });
            var check = UiKit.Icon("Check", 18, UiKit.Blue);
            check.Visibility = selected ? Visibility.Visible : Visibility.Hidden;
            Grid.SetColumn(check, 1);
            row.Children.Add(check);

            var option = new Button
            {
                Style = (Style)System.Windows.Application.Current.FindResource("ShellButton"),
                Background = selected ? UiKit.B("#E6F0FD") : Brushes.White, BorderBrush = Brushes.Transparent,
                Foreground = UiKit.Ink, Height = 40, Margin = new Thickness(0, 1, 0, 1), Padding = new Thickness(12, 0, 12, 0), Content = row
            };
            option.Click += (_, _) =>
            {
                popup.IsOpen = false;
                AppearanceManager.Current.SetLanguage(code);   // saved, then every open window (this one included) is refreshed
            };
            return option;
        }

        // ------------------------------------------------------------------ navigation

        private void Navigate(string key)
        {
            if (!Allowed(key)) { ShowDenied(); return; }

            switch (key)
            {
                case "Sales": OpenDialog(T("تعذر فتح شاشة المبيعات", "Could not open Sales"), () => ShowDialogWindow(App.Services.GetRequiredService<SalesWindow>())); return;
                case "Returns": OpenDialog(T("تعذر فتح شاشة المرتجعات", "Could not open Returns"), () => ShowDialogWindow(App.Services.GetRequiredService<ReturnsWindow>())); return;
                case "Roles": OpenDialog(T("تعذر فتح شاشة الأدوار", "Could not open Roles"), OpenRoles); return;
                case "Accounts": OpenDialog(T("تعذر فتح حسابات الدخول", "Could not open login accounts"), () =>
                    ShowDialogWindow(new UserAccountsWindow(App.Services.GetRequiredService<UserAccountManagementService>(), _permissionChecker))); return;
            }

            _currentKey = key;
            HighlightNav();
            ShowPage(key);
        }

        private void ShowDialogWindow(Window window)
        {
            window.Owner = this;
            window.ShowDialog();
        }

        private void OpenRoles()
        {
            // The roles screen works on its own DbContext, disposed together with the dialog.
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ShowDialogWindow(new RolePermissionsWindow(new RolePermissionManagementService(db, _session)));
        }

        private void OpenDialog(string errorTitle, Action open)
        {
            try { open(); }
            catch (Exception ex)
            {
                MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowPage(string key)
        {
            var oldScope = _pageScope;
            _pageScope = null;
            try
            {
                UIElement page = key switch
                {
                    "Home" => new HomePage(_session.DisplayName ?? string.Empty, HomeTiles()),
                    "Employees" => new EmployeesPage(App.Services.GetRequiredService<EmployeeService>(), _permissionChecker),
                    "Products" when IsCashier => SectionPage("LowStock", T("المنتجات القريبة من النفاد", "Low-stock products")),
                    "Products" => new ProductsPage(NewScopeDb()),
                    "Purchases" => new PurchasesPage(NewScopeDb()),
                    "Reports" => new ReportsPage(NewScopeDb()),
                    "Settings" => new SettingsPage(_session, ShowMyPermissions),
                    "Suppliers" => SectionPage("Suppliers", T("الموردون", "Suppliers")),
                    "Inventory" => SectionPage("Inventory", T("المخزون والجرد", "Inventory & stocktaking")),
                    "StockMovements" => SectionPage("StockMovements", T("حركات المخزون", "Stock movements")),
                    "Shifts" => SectionPage("Shifts", T("الورديات والعهدة", "Shifts & cash")),
                    "LowStock" => SectionPage("LowStock", T("الأصناف منخفضة المخزون", "Low-stock products")),
                    _ => new HomePage(_session.DisplayName ?? string.Empty, HomeTiles())
                };
                PageHost.Content = page;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Could not open page '{key}': {ex}");
                PageHost.Content = new TextBlock
                {
                    Text = ErrorMessageMapper.ToUserMessage(ex), Foreground = Brushes.Firebrick, FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8)
                };
            }
            finally
            {
                oldScope?.Dispose();   // release the previous screen's DbContext after the new page replaced it
            }
        }

        private AppDbContext NewScopeDb()
        {
            // Each data screen gets its own DbContext, kept alive while the screen is shown (same rule as the old section windows).
            _pageScope?.Dispose();
            _pageScope = App.Services.CreateScope();
            return _pageScope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        private UIElement SectionPage(string section, string title) => new SectionView(NewScopeDb(), section, title);

        private void ShowSection(string section, string title)
        {
            _currentKey = IsCashier ? "Products" : "Inventory";
            HighlightNav();
            var oldScope = _pageScope;
            _pageScope = null;
            try { PageHost.Content = SectionPage(section, title); }
            catch (Exception ex) { MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), T("تعذر فتح الشاشة", "Could not open screen"), MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { oldScope?.Dispose(); }
        }

        private IEnumerable<HomeTile> HomeTiles()
        {
            var descriptions = new Dictionary<string, (string Ar, string En)>
            {
                ["Employees"] = ("بيانات الموظفين وأقسامهم", "Employee records and departments"),
                ["Products"] = ("الأصناف والأسعار والتصنيفات", "Items, prices and categories"),
                ["Purchases"] = ("فواتير الشراء من الموردين", "Purchase invoices from suppliers"),
                ["Sales"] = ("إنشاء الفواتير وإتمام البيع", "Create invoices and complete sales"),
                ["Reports"] = ("ملخصات المبيعات والمشتريات", "Sales and purchase summaries"),
                ["Settings"] = ("اللغة وبيانات حسابك", "Language and your account"),
                ["Suppliers"] = ("بيانات الموردين", "Supplier records"),
                ["Inventory"] = ("أرصدة المخزون والجرد", "Stock balances and stocktaking"),
                ["StockMovements"] = ("سجل حركات المخزون", "Stock movement history"),
                ["Shifts"] = ("متابعة النقدية والورديات", "Track cash and shifts"),
                ["Returns"] = ("إدارة مرتجعات البيع", "Manage sales returns"),
                ["Roles"] = ("تحديد صلاحيات كل دور", "Set the permissions of each role"),
                ["Accounts"] = ("إدارة حسابات الدخول", "Manage login accounts"),
            };
            foreach (var item in NavItems)
            {
                if (item.Key == "Home" || !Allowed(item.Key)) continue;
                var key = item.Key;
                var (ar, en) = descriptions.TryGetValue(key, out var d) ? d : ("", "");
                yield return new HomeTile(item.Icon, T(item.Arabic, item.English), T(ar, en), () => Navigate(key));
            }
        }

        private void ShowMyPermissions()
        {
            var message = new System.Text.StringBuilder();
            message.AppendLine(T($"المستخدم: {_session.DisplayName}", $"User: {_session.DisplayName}"));
            message.AppendLine(T($"الأدوار: {string.Join(", ", _session.Roles)}", $"Roles: {string.Join(", ", _session.Roles)}"));
            message.AppendLine();
            if (IsAdmin)
                message.AppendLine(T("حساب Admin لديه صلاحية الوصول لجميع أقسام النظام.", "The Admin account can access all system sections."));
            else if (_session.Permissions.Count == 0)
                message.AppendLine(T("لا توجد صلاحيات مسجلة لهذا الحساب.", "No permissions are registered for this account."));
            else
            {
                message.AppendLine(T("رموز الصلاحيات الممنوحة لحسابك:", "Your assigned permission codes:"));
                foreach (string permission in _session.Permissions.OrderBy(p => p)) message.AppendLine($"• {permission}");
            }
            MessageBox.Show(message.ToString(), T("صلاحياتي", "My permissions"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Logout()
        {
            var result = MessageBox.Show(T("هل تريد تسجيل الخروج؟", "Do you want to sign out?"), T("تسجيل الخروج", "Sign out"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // Offer to close an open cash shift before leaving. Declining keeps the shift open;
            // it is restored (never duplicated) the next time this user signs in.
            if (_session.CashShiftId is int openShiftId)
            {
                var loc = LocalizationManager.Current;
                var closeAnswer = MessageBox.Show(loc["CloseShiftPrompt"], loc["CloseShiftTitle"], MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (closeAnswer == MessageBoxResult.Cancel) return;
                if (closeAnswer == MessageBoxResult.Yes)
                {
                    var closeWindow = new CloseShiftWindow(
                        App.Services.GetRequiredService<UniversalPOS.Application.CashManagement.CloseCashShiftService>(),
                        _permissionChecker, openShiftId) { Owner = this };
                    if (closeWindow.ShowDialog() != true) return;
                }
            }

            _session.SignOut();
            var loginWindow = App.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            Close();
        }

        private void ShowDenied()
        {
            MessageBox.Show(T("ليس لديك صلاحية لتنفيذ هذا الإجراء.", "You do not have permission to perform this action."),
                T("صلاحيات غير كافية", "Insufficient permissions"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
