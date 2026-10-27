using System.Globalization;
using Microsoft.AspNetCore.Localization;
using UniversalPOS.Api.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantConnectionStringResolver, TenantConnectionStringResolver>();
builder.Services.AddHealthChecks();

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("en-US"), new CultureInfo("ar-EG") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    FallBackToParentCultures = true,
    FallBackToParentUICultures = true
});

app.MapHealthChecks("/health");
app.MapGet("/api/system/cultures", () => Results.Ok(new[]
{
    new { Code = "en-US", DisplayName = "English", IsRightToLeft = false },
    new { Code = "ar-EG", DisplayName = "العربية", IsRightToLeft = true }
}));

app.Run();

public partial class Program { }
