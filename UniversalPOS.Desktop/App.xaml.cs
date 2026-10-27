using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
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

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json",optional: false,
                        reloadOnChange: true).AddUserSecrets<App>().Build();
                var services = new ServiceCollection();

                // Configuration
                services.AddSingleton<IConfiguration>(configuration);

                // Database
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(configuration.GetConnectionString("NexusPOS")));

                // Repositories
                services.AddScoped<ISaleRepository, SaleRepository>();
                services.AddScoped<IProductRepository, ProductRepository>();
                services.AddScoped<IStockRepository, StockRepository>();
                services.AddScoped<IUnitOfWork, UnitOfWork>();
                services.AddScoped<IUserRepository, UserRepository>();
                services.AddScoped<IPosTerminalRepository, PosTerminalRepository>();
                services.AddScoped<ICashShiftRepository, CashShiftRepository>();
                services.AddScoped<ISaleReturnRepository, SaleReturnRepository>();
                services.AddScoped<IAuditLogRepository, AuditLogRepository>();
                services.AddScoped<IEmployeeRepository, EmployeeRepository>();
                services.AddScoped<UniversalPOS.Application.Employees.EmployeeService>();
                services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();

                // Current user session
                services.AddSingleton<CurrentUserSession>();

                // Authentication and security
                services.AddScoped<LoginService>();
                services.AddScoped<PermissionChecker>();

                // Cash management
                services.AddScoped<OpenCashShiftService>();
                services.AddScoped<CloseCashShiftService>();
                services.AddScoped<ApproveShiftDifferenceService>();
                services.AddScoped<ActiveShiftService>();

                // Sales
                services.AddScoped<CreateSaleService>();
                services.AddScoped<AddProductToSaleService>();
                services.AddScoped<GetSaleDetailsService>();
                services.AddScoped<RegisterPaymentService>();
                services.AddScoped<CompleteSaleService>();
                services.AddScoped<CancelSaleService>();
                services.AddScoped<SaleAdjustmentService>();
                services.AddScoped<UniversalPOS.Application.Settings.SettingsService>();
                services.AddScoped<ProcessSaleReturnService>();

                // Products and inventory
                services.AddScoped<GetProductsService>();
                services.AddScoped<StockService>();

                // Initial data and permissions
                services.AddScoped<PermissionSeeder>();
                services.AddScoped<InitialDataSeeder>();

                // Windows
                services.AddTransient<LoginWindow>();
                services.AddTransient<OpenShiftWindow>();
                services.AddTransient<MainWindow>();
                services.AddTransient<SalesWindow>();
                services.AddTransient<ReturnsWindow>();

                // Build service provider
                Services = services.BuildServiceProvider();

                // Seed initial data
                using (var scope = Services.CreateScope())
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<InitialDataSeeder>();
                    await seeder.SeedAsync();
                }

                // Start with LoginWindow
                var loginWindow = Services.GetRequiredService<LoginWindow>();
                System.Windows.Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(),"Application Startup Error",MessageBoxButton.OK,MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
    }
}