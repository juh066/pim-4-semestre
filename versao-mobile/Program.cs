using AuroraGaleria.Data;
using AuroraGaleria.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore; 
using Microsoft.AspNetCore.Mvc;
using AuroraGaleria.Services;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AcademicDemoOptions>(
    builder.Configuration.GetSection(AcademicDemoOptions.SectionName));
builder.Services.Configure<GalleryOptions>(builder.Configuration.GetSection(GalleryOptions.SectionName));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/auth") ||
                context.Request.Path.StartsWithSegments("/user-actions") ||
                context.Request.Path.StartsWithSegments("/cart") ||
                context.Request.Path.StartsWithSegments("/privacy"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var idValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var version = context.Principal?.FindFirst("session_version")?.Value;
            if (!int.TryParse(idValue, out var id)) { context.RejectPrincipal(); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var valid = await db.Users.AsNoTracking().AnyAsync(user => user.Id == id && user.IsActive && user.SessionVersion == version);
            if (!valid) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(); }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IAdminCatalogService, AdminCatalogService>();
builder.Services.AddScoped<AdminBootstrapService>();
builder.Services.AddScoped<IPrivacyService, PrivacyService>();
builder.Services.AddScoped<IRetentionService, RetentionService>();
builder.Services.AddScoped<SqliteToSqlServerMigrator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGalleryClock, GalleryClock>();

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("A conexão DefaultConnection não foi configurada.");
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
});

var app = builder.Build();

var importingFromSqlite = args.Contains("--migrate-sqlite-to-sqlserver", StringComparer.OrdinalIgnoreCase);
await DatabaseInitializer.InitializeAsync(app.Services, app.Logger, seedCatalog: !importingFromSqlite);

if (importingFromSqlite || args.Contains("--verify-sqlserver-import", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<SqliteToSqlServerMigrator>();
    var result = importingFromSqlite
        ? await migrator.ImportAsync()
        : await migrator.VerifyAsync();
    Console.WriteLine(result.Message);
    Environment.ExitCode = result.Success ? 0 : 1;
    return;
}

if (args.Contains("--retention-preview", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("--retention-cleanup", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var retention = scope.ServiceProvider.GetRequiredService<IRetentionService>();
    var preview = await retention.PreviewAsync();
    Console.WriteLine(preview.Message);
    if (args.Contains("--retention-cleanup", StringComparer.OrdinalIgnoreCase))
    {
        var confirmed = args.Contains("--confirm-retention-cleanup", StringComparer.OrdinalIgnoreCase);
        var result = await retention.ExecuteAsync(confirmed);
        Console.WriteLine(result.Message);
        Environment.ExitCode = result.Success ? 0 : 1;
    }
    return;
}

if (args.Contains("--create-admin", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<AdminBootstrapService>();
    var result = await service.CreateFirstAsync(
        Environment.GetEnvironmentVariable("AURORA_ADMIN_NAME") ?? string.Empty,
        Environment.GetEnvironmentVariable("AURORA_ADMIN_EMAIL") ?? string.Empty,
        Environment.GetEnvironmentVariable("AURORA_ADMIN_PASSWORD") ?? string.Empty);
    Console.WriteLine(result.Message);
    Environment.ExitCode = result.Success ? 0 : 1;
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();

public partial class Program;
