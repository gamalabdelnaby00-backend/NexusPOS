using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.CashManagement;
using UniversalPOS.Application.Sales;
using UniversalPOS.Application.SalesReturns;
using UniversalPOS.Application.Security;
using UniversalPOS.Application.Stock;
using UniversalPOS.Desktop.Views;
using UniversalPOS.Infrastructure.Data;
using UniversalPOS.Infrastructure.Data.Seeding;
using UniversalPOS.Infrastructure.Repositories;

namespace UniversalPOS.Desktop
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        // Startup work runs after the login window is visible. Login awaits this task
        // before authenticating, so seeding cannot race the first login attempt.
        public static Task StartupInitializationTask { get; private set; } = Task.CompletedTask;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            AppearanceManager.Current.Load();
            try
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json",optional: false,
                        reloadOnChange: true).AddUserSecrets<App>().Build();
                var services = new ServiceCollection();

                // Configuration
                services.AddSingleton<IConfiguration>(configuration);

                // Database
                var configuredConnectionString = configuration.GetConnectionString("NexusPOS");
                if (string.IsNullOrWhiteSpace(configuredConnectionString))
                    throw new InvalidOperationException(
                        "Connection string 'NexusPOS' is missing. Configure it in User Secrets.");

                // SQL Server may need longer than the default 15 seconds during startup/login.
                // Preserve the existing server/database/credentials; only extend connection timeout.
                var sqlConnectionBuilder = new SqlConnectionStringBuilder(configuredConnectionString)
                {
                    ConnectTimeout = Math.Max(60, new SqlConnectionStringBuilder(configuredConnectionString).ConnectTimeout)
                };
                // Each context instance must be isolated from other UI operations. Operational
                // screens also run inside a dedicated scope; transient registration avoids a
                // root-resolved window accidentally capturing one app-wide scoped context.
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(sqlConnectionBuilder.ConnectionString, sql => sql.CommandTimeout(60)),
                    contextLifetime: ServiceLifetime.Transient,
                    optionsLifetime: ServiceLifetime.Singleton);

                // Repositories
                services.AddTransient<ISaleRepository, SaleRepository>();
                services.AddTransient<IProductRepository, ProductRepository>();
                services.AddTransient<IStockRepository, StockRepository>();
                services.AddTransient<IUnitOfWork, UnitOfWork>();
                services.AddTransient<IUserRepository, UserRepository>();
                services.AddTransient<IPosTerminalRepository, PosTerminalRepository>();
                services.AddTransient<ICashShiftRepository, CashShiftRepository>();
                services.AddTransient<ISaleReturnRepository, SaleReturnRepository>();
                services.AddTransient<IAuditLogRepository, AuditLogRepository>();
                services.AddTransient<IEmployeeRepository, EmployeeRepository>();
                services.AddTransient<UniversalPOS.Application.Employees.EmployeeService>();
                services.AddTransient<ISystemSettingsRepository, SystemSettingsRepository>();

                // Current user session
                services.AddSingleton<CurrentUserSession>();

                // Authentication and security
                services.AddTransient<LoginService>();
                services.AddTransient<UserAccountManagementService>();
                services.AddTransient<PermissionChecker>();

                // Cash management
                services.AddTransient<OpenCashShiftService>();
                services.AddTransient<CloseCashShiftService>();
                services.AddTransient<RegisterCashMovementService>();
                services.AddTransient<ApproveShiftDifferenceService>();
                services.AddTransient<ActiveShiftService>();

                // Sales
                services.AddTransient<CreateSaleService>();
                services.AddTransient<AddProductToSaleService>();
                services.AddTransient<GetSaleDetailsService>();
                services.AddTransient<RegisterPaymentService>();
                services.AddTransient<CompleteSaleService>();
                services.AddTransient<CancelSaleService>();
                services.AddTransient<SaleAdjustmentService>();
                services.AddTransient<UniversalPOS.Application.Settings.SettingsService>();
                services.AddTransient<ProcessSaleReturnService>();

                // Products and inventory
                services.AddTransient<GetProductsService>();
                services.AddTransient<StockService>();

                // Initial data and permissions
                services.AddTransient<PermissionSeeder>();
                services.AddTransient<InitialDataSeeder>();

                // Windows
                services.AddTransient<LoginWindow>();
                services.AddTransient<OpenShiftWindow>();
                services.AddTransient<MainWindow>();
                services.AddTransient<SalesWindow>();
                services.AddTransient<ReturnsWindow>();

                // Build service provider
                Services = services.BuildServiceProvider();

                // Display login immediately instead of blocking the first window on SQL Server.
                // LoginWindow awaits this task before checking credentials, avoiding a race with seeding.
                var loginWindow = Services.GetRequiredService<LoginWindow>();
                System.Windows.Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
                StartupInitializationTask = InitializeStartupDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Application startup failed: " + ex);
                MessageBox.Show(
                    "تعذر تشغيل البرنامج. راجع سجل الأخطاء أو إعدادات الاتصال بقاعدة البيانات.",
                    "خطأ في بدء التشغيل",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
        private static async Task InitializeStartupDataAsync()
        {
            try
            {
                using var scope = Services.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<InitialDataSeeder>();
                await seeder.SeedAsync();
            }
            catch (Exception seedException)
            {
                System.Diagnostics.Trace.WriteLine("Initial data seeding failed: " + seedException);
                // The login view is already visible. The login attempt will still report a
                // user-safe error if required startup data cannot be reached.
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine("Unhandled UI exception: " + e.Exception);
            MessageBox.Show(
                "حدث خطأ غير متوقع. لم يتم عرض التفاصيل التقنية لحماية بيانات النظام. " +
                "إذا تكررت المشكلة، راجع سجل الأخطاء وأعد المحاولة بعد التأكد من حالة العملية.",
                "خطأ غير متوقع",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
                System.Diagnostics.Trace.WriteLine("Unhandled application exception: " + exception);
            else
                System.Diagnostics.Trace.WriteLine("Unhandled application exception: " + e.ExceptionObject);
        }

    }
}