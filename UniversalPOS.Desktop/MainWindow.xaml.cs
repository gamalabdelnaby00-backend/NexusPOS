
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Security;
using UniversalPOS.Desktop.Views;

namespace UniversalPOS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly CurrentUserSession _session;
        private readonly PermissionChecker _permissionChecker;

        private bool _isInitializingPreferences = true;
        private string _currentLanguage = "ar";
        private string _currentTheme = "Light";

        public MainWindow()
        {
            InitializeComponent();

            _session = App.Services.GetRequiredService<CurrentUserSession>();
            _permissionChecker = App.Services.GetRequiredService<PermissionChecker>();

            Loaded += MainWindow_Loaded;
        }

        private bool IsAdmin =>
            _session.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

        private bool IsCashier =>
            _session.Roles.Contains("Cashier", StringComparer.OrdinalIgnoreCase);

        private string T(string arabic, string english) =>
            _currentLanguage == "ar" ? arabic : english;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_session.IsAuthenticated)
            {
                MessageBox.Show(
                    T("يجب تسجيل الدخول أولاً.", "Please sign in first."),
                    T("تسجيل الدخول", "Sign in"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Close();
                return;
            }

            WelcomeText.Text = T(
                $"أهلاً بك، {_session.DisplayName}",
                $"Welcome, {_session.DisplayName}");

            RoleText.Text = T(
                $"اسم المستخدم: {_session.UserName} | الأدوار: {string.Join(", ", _session.Roles)}",
                $"User: {_session.UserName} | Roles: {string.Join(", ", _session.Roles)}");

            ConfigurePermissions();

            _isInitializingPreferences = false;

            ApplyLanguage();
            ApplyTheme();
        }

        private bool Can(params string[] codes)
        {
            if (!_session.IsAuthenticated)
                return false;

            if (IsAdmin)
                return true;

            return codes.Any(code => _permissionChecker.HasPermission(code));
        }

        private void SetSection(
            Button navigationButton,
            Button cardButton,
            bool visible)
        {
            Visibility visibility = visible
                ? Visibility.Visible
                : Visibility.Collapsed;

            navigationButton.Visibility = visibility;
            cardButton.Visibility = visibility;
        }

        private void ConfigurePermissions()
        {
            // Current approved cashier policy: sales and returns are always
            // available to authenticated Cashier accounts, without a separate
            // manager approval or an extra permission assignment.
            SetSection(SalesButton, SalesCardButton,
                IsCashier || Can("Sales.View", "Sales.Create", "Sales.CreateSale"));

            SetSection(ReturnsButton, ReturnsCardButton,
                IsCashier || Can("Returns.View", "Returns.Create", "Returns.Process"));

            SetSection(ShiftsButton, ShiftsCardButton,
                Can("Shifts.View", "Shifts.Open", "Shifts.Close",
                    "CashShifts.View", "CashShifts.Open"));

            SetSection(ProductsButton, ProductsCardButton,
                IsCashier || Can("Products.View", "Products.Create", "Products.Update"));

            SetSection(InventoryButton, InventoryCardButton,
                Can("Inventory.View", "Inventory.Adjust", "Inventory.Manage"));

            SetSection(PurchasesButton, PurchasesCardButton,
                Can("Purchases.View", "Purchases.Create", "Suppliers.View",
                    "SupplierPayments.View"));

            SetSection(ReportsButton, ReportsCardButton,
                Can("Reports.View", "Reports.Sales", "Reports.Inventory"));

            SetSection(UsersButton, UsersCardButton,
                Can("Users.View", "Users.Create", "Users.Manage",
                    "Roles.View", "Roles.Manage", "Permissions.View"));

            bool canViewOwnPermissions = !IsCashier;
            PermissionsButton.Visibility = canViewOwnPermissions ? Visibility.Visible : Visibility.Collapsed;
            PermissionsCardButton.Visibility = canViewOwnPermissions ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LanguageComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_isInitializingPreferences ||
                LanguageComboBox.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            _currentLanguage = item.Tag?.ToString() == "en"
                ? "en"
                : "ar";

            ApplyLanguage();
            ApplyComboBoxTheme(LanguageComboBox, _currentTheme == "Dark");
            ApplyComboBoxTheme(ThemeComboBox, _currentTheme == "Dark");
        }

        private void ApplyLanguage()
        {
            bool arabic = _currentLanguage == "ar";

            RootGrid.FlowDirection = arabic
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            Title = T("NexusPOS - الرئيسية", "NexusPOS - Dashboard");

            BrandText.Text = "NexusPOS";
            BrandSubtitleText.Text = T(
                "نظام إدارة نقطة البيع",
                "Point of Sale Management");

            PageTitleText.Text = T("لوحة التحكم", "Dashboard");
            LanguageLabel.Text = T("اللغة", "Language");
            ThemeLabel.Text = T("المظهر", "Appearance");

            HomeButton.Content = T("الرئيسية", "Home");
            SalesButton.Content = T("المبيعات والفواتير", "Sales & Invoices");
            ReturnsButton.Content = T("المرتجعات", "Returns");
            ShiftsButton.Content = T("الورديات والعهدة", "Shifts & Cash");
            ProductsButton.Content = T("المنتجات والتصنيفات", "Products & Categories");
            InventoryButton.Content = T("المخزون والجرد", "Inventory & Stocktaking");
            PurchasesButton.Content = T("المشتريات والموردون", "Purchases & Suppliers");
            ReportsButton.Content = T("التقارير", "Reports");
            UsersButton.Content = T("المستخدمون والصلاحيات", "Users & Permissions");
            PermissionsButton.Content = T("صلاحياتي", "My Permissions");
            LogoutButton.Content = T("تسجيل الخروج", "Sign out");

            OverviewTitle.Text = T("نظرة عامة", "Overview");
            SectionsTitle.Text = T("الأقسام المتاحة لحسابك", "Available Sections");

            SalesMetricTitle.Text = T("مبيعات اليوم", "Today's Sales");
            SalesMetricNote.Text = T(
                "سيتم ربطها ببيانات المبيعات",
                "Will be connected to sales data");

            InventoryMetricTitle.Text = T("المخزون", "Inventory");
            InventoryMetricNote.Text = T("ملخص المخزون", "Inventory overview");

            PurchasesMetricTitle.Text = T("المشتريات", "Purchases");
            PurchasesMetricNote.Text = T("ملخص المشتريات", "Purchases overview");

            SalesCardTitle.Text = T("المبيعات والفواتير", "Sales & Invoices");
            SalesCardDescription.Text = T(
                "إنشاء الفواتير وإتمام البيع",
                "Create invoices and complete sales");

            ReturnsCardTitle.Text = T("المرتجعات", "Returns");
            ReturnsCardDescription.Text = T(
                "إدارة مرتجعات البيع",
                "Manage sales returns");

            ShiftsCardTitle.Text = T("الورديات والعهدة", "Shifts & Cash");
            ShiftsCardDescription.Text = T(
                "متابعة النقدية والورديات",
                "Track cash and shifts");

            ProductsCardTitle.Text = T(
                "المنتجات والتصنيفات",
                "Products & Categories");

            ProductsCardDescription.Text = T(
                "إدارة المنتجات والأسعار",
                "Manage products and prices");

            InventoryCardTitle.Text = T("المخزون والجرد", "Inventory & Stocktaking");
            InventoryCardDescription.Text = T(
                "متابعة الكميات والحركات",
                "Track quantities and movements");

            PurchasesCardTitle.Text = T(
                "المشتريات والموردون",
                "Purchases & Suppliers");

            PurchasesCardDescription.Text = T(
                "إدارة دورة الشراء",
                "Manage the purchasing cycle");

            ReportsCardTitle.Text = T("التقارير", "Reports");
            ReportsCardDescription.Text = T(
                "تقارير حسب الصلاحيات",
                "Reports based on permissions");

            UsersCardTitle.Text = T(
                "المستخدمون والصلاحيات",
                "Users & Permissions");

            UsersCardDescription.Text = T(
                "إدارة الحسابات والأدوار",
                "Manage accounts and roles");

            PermissionsCardTitle.Text = T("صلاحياتي", "My Permissions");
            PermissionsCardDescription.Text = T(
                "عرض الصلاحيات الممنوحة لحسابك",
                "View your assigned permissions");

            LanguageComboBox.FlowDirection = FlowDirection.LeftToRight;
            ThemeComboBox.FlowDirection = FlowDirection.LeftToRight;
        }

        private void ThemeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_isInitializingPreferences ||
                ThemeComboBox.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            _currentTheme = item.Tag?.ToString() == "Dark"
                ? "Dark"
                : "Light";

            ApplyTheme();
        }

        private void ApplyTheme()
        {
            bool dark = _currentTheme == "Dark";

            Color page = GetColor(dark ? "#111827" : "#F3F5F9");
            Color panel = GetColor(dark ? "#1F2937" : "#FFFFFF");
            Color text = GetColor(dark ? "#F9FAFB" : "#17283F");
            Color secondary = GetColor(dark ? "#C0CAD8" : "#52647A");
            Color border = GetColor(dark ? "#4B5563" : "#E0E6EF");
            Color sidebar = GetColor(dark ? "#0B1220" : "#17283F");

            // يتم تحديث Tag أولاً حتى تقرأه الـ Styles.
            Tag = dark ? "Dark" : "Light";

            RootGrid.Background = Brush(page);
            Background = Brush(page);

            Sidebar.Background = Brush(sidebar);
            TopBar.Background = Brush(panel);
            TopBar.BorderBrush = Brush(border);

            PageTitleText.Foreground = Brush(text);
            WelcomeText.Foreground = Brush(secondary);
            RoleText.Foreground = Brush(secondary);
            OverviewTitle.Foreground = Brush(text);
            SectionsTitle.Foreground = Brush(text);
            LanguageLabel.Foreground = Brush(secondary);
            ThemeLabel.Foreground = Brush(secondary);

            ApplyMetricTheme(
                SalesMetricCard, SalesMetricTitle, SalesMetricNote,
                panel, border, secondary);

            ApplyMetricTheme(
                InventoryMetricCard, InventoryMetricTitle, InventoryMetricNote,
                panel, border, secondary);

            ApplyMetricTheme(
                PurchasesMetricCard, PurchasesMetricTitle, PurchasesMetricNote,
                panel, border, secondary);

            Button[] cards =
            {
                SalesCardButton, ReturnsCardButton, ShiftsCardButton,
                ProductsCardButton, InventoryCardButton, PurchasesCardButton,
                ReportsCardButton, UsersCardButton, PermissionsCardButton
            };

            foreach (Button card in cards)
            {
                card.Background = Brush(panel);
                card.Foreground = Brush(text);
                card.BorderBrush = Brush(border);

                foreach (TextBlock block in FindVisualChildren<TextBlock>(card))
                {
                    block.Foreground = block.FontSize <= 14
                        ? Brush(secondary)
                        : Brush(text);
                }
            }

            Button[] navigation =
            {
                HomeButton, SalesButton, ReturnsButton, ShiftsButton,
                ProductsButton, InventoryButton, PurchasesButton,
                ReportsButton, UsersButton, PermissionsButton, LogoutButton
            };

            foreach (Button button in navigation)
            {
                button.Background = Brush(sidebar);
                button.Foreground = Brush(GetColor("#E7EDF5"));
            }

            ApplyComboBoxTheme(LanguageComboBox, dark);
            ApplyComboBoxTheme(ThemeComboBox, dark);
        }

        private void ApplyComboBoxTheme(ComboBox comboBox, bool dark)
        {
            Brush foreground = Brush(GetColor(
                dark ? "#FFFFFF" : "#17283F"));

            Brush background = Brush(GetColor(
                dark ? "#1F2937" : "#FFFFFF"));

            Brush border = Brush(GetColor(
                dark ? "#4B5563" : "#D1D5DB"));

            comboBox.Foreground = foreground;
            comboBox.Background = background;
            comboBox.BorderBrush = border;

            // إزالة القيم المحلية حتى يطبّق ItemContainerStyle ألوانه.
            foreach (object item in comboBox.Items)
            {
                if (item is ComboBoxItem comboItem)
                {
                    comboItem.ClearValue(Control.ForegroundProperty);
                    comboItem.ClearValue(Control.BackgroundProperty);
                }
            }

            // تحديث الـ Style بعد تغيير Tag الخاص بالنافذة.
            comboBox.InvalidateProperty(Control.ForegroundProperty);
            comboBox.InvalidateProperty(Control.BackgroundProperty);
            comboBox.InvalidateVisual();
        }

        private static void ApplyMetricTheme(
            Border card,
            TextBlock title,
            TextBlock note,
            Color background,
            Color border,
            Color secondary)
        {
            card.Background = Brush(background);
            card.BorderBrush = Brush(border);
            title.Foreground = Brush(secondary);
            note.Foreground = Brush(secondary);
        }

        private static Color GetColor(string hex) =>
            (Color)ColorConverter.ConvertFromString(hex);

        private static SolidColorBrush Brush(Color color) =>
            new SolidColorBrush(color);

        private static System.Collections.Generic.IEnumerable<T>
            FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
                yield break;

            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typed)
                    yield return typed;

                foreach (T descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            // أنت بالفعل على الشاشة الرئيسية.
        }

        private void SalesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCashier && !Can("Sales.View", "Sales.Create", "Sales.CreateSale"))
            {
                ShowDenied();
                return;
            }

            try
            {
                SalesWindow window =
                    App.Services.GetRequiredService<SalesWindow>();

                // Keep the dashboard alive behind the invoice so closing the
                // invoice returns the user to the dashboard instead of exiting.
                window.Owner = this;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    T("تعذر فتح شاشة المبيعات", "Could not open Sales"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ReturnsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCashier && !Can("Returns.View", "Returns.Create", "Returns.Process"))
            {
                ShowDenied();
                return;
            }

            try
            {
                var window = App.Services.GetRequiredService<ReturnsWindow>();
                window.Owner = this;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    T("تعذر فتح شاشة المرتجعات", "Could not open Returns"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShiftsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Can("Shifts.View", "Shifts.Open", "Shifts.Close", "CashShifts.View", "CashShifts.Open")) { ShowDenied(); return; }
            OpenSection("Shifts", T("الورديات والعهدة", "Shifts & Cash"));
        }

        private void ProductsButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsCashier) { OpenSection("LowStock", T("المنتجات القريبة من النفاد", "Low-stock products")); return; }
            if (!Can("Products.View", "Products.Create", "Products.Update")) { ShowDenied(); return; }
            OpenSection("Products", T("المنتجات والتصنيفات", "Products & Categories"));
        }

        private void InventoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Can("Inventory.View", "Inventory.Adjust", "Inventory.Manage")) { ShowDenied(); return; }
            OpenSection("Inventory", T("المخزون والجرد", "Inventory & Stocktaking"));
        }

        private void PurchasesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Can("Purchases.View", "Purchases.Create", "Suppliers.View", "SupplierPayments.View")) { ShowDenied(); return; }
            OpenSection("Purchases", T("المشتريات والموردون", "Purchases & Suppliers"));
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Can("Reports.View", "Reports.Sales", "Reports.Inventory")) { ShowDenied(); return; }
            OpenSection("Reports", T("التقارير", "Reports"));
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Can("Users.View", "Users.Create", "Users.Manage", "Roles.View", "Roles.Manage", "Permissions.View")) { ShowDenied(); return; }
            try
            {
                var window = new UsersWindow(
                    App.Services.GetRequiredService<UniversalPOS.Application.Employees.EmployeeService>(),
                    _permissionChecker) { Owner = this };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), T("تعذر فتح الشاشة", "Could not open screen"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PermissionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_session.IsAuthenticated)
            {
                ShowDenied();
                return;
            }

            StringBuilder message = new StringBuilder();

            message.AppendLine(T(
                $"المستخدم: {_session.DisplayName}",
                $"User: {_session.DisplayName}"));

            message.AppendLine(T(
                $"الأدوار: {string.Join(", ", _session.Roles)}",
                $"Roles: {string.Join(", ", _session.Roles)}"));

            message.AppendLine();

            if (IsAdmin)
            {
                message.AppendLine(T(
                    "حساب Admin لديه صلاحية الوصول لجميع أقسام النظام.",
                    "The Admin account can access all system sections."));
            }
            else if (_session.Permissions.Count == 0)
            {
                message.AppendLine(T(
                    "لا توجد صلاحيات مسجلة لهذا الحساب.",
                    "No permissions are registered for this account."));
            }
            else
            {
                message.AppendLine(T(
                    "رموز الصلاحيات الممنوحة لحسابك:",
                    "Your assigned permission codes:"));

                foreach (string permission in _session.Permissions.OrderBy(p => p))
                    message.AppendLine($"• {permission}");
            }

            MessageBox.Show(
                message.ToString(),
                T("صلاحياتي", "My Permissions"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                T("هل تريد تسجيل الخروج؟", "Do you want to sign out?"),
                T("تسجيل الخروج", "Sign out"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Offer to close an open cash shift before leaving. Declining keeps the shift open;
            // it is restored (never duplicated) the next time this user signs in.
            if (_session.CashShiftId is int openShiftId)
            {
                var loc = LocalizationManager.Current;
                var closeAnswer = MessageBox.Show(
                    loc["CloseShiftPrompt"],
                    loc["CloseShiftTitle"],
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (closeAnswer == MessageBoxResult.Cancel)
                    return;

                if (closeAnswer == MessageBoxResult.Yes)
                {
                    var closeWindow = new CloseShiftWindow(
                        App.Services.GetRequiredService<UniversalPOS.Application.CashManagement.CloseCashShiftService>(),
                        _permissionChecker,
                        openShiftId)
                    { Owner = this };

                    if (closeWindow.ShowDialog() != true)
                        return;
                }
            }

            _session.SignOut();

            LoginWindow loginWindow =
                App.Services.GetRequiredService<LoginWindow>();

            loginWindow.Show();
            Close();
        }

        private void OpenSection(string section, string title)
        {
            try
            {
                var db = App.Services.GetRequiredService<UniversalPOS.Infrastructure.Data.AppDbContext>();
                var window = new SectionWindow(db, section, title) { Owner = this };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, T("تعذر فتح الشاشة", "Could not open screen"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDenied()
        {
            MessageBox.Show(
                T(
                    "ليس لديك صلاحية لتنفيذ هذا الإجراء.",
                    "You do not have permission to perform this action."),
                T("صلاحيات غير كافية", "Insufficient permissions"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
