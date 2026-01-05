using GUMS.Components;
using GUMS.Data;
using GUMS.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Initialize SQLCipher native library
SQLitePCL.Batteries_V2.Init();

var builder = WebApplication.CreateBuilder(args);

// Register encryption service first (as singleton so we can resolve it early)
builder.Services.AddSingleton<IDatabaseEncryptionService, DatabaseEncryptionService>();

// Configure database path
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "GUMS", "gums.db");

// Ensure directory exists
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

// Get or create database encryption key
var encryptionServiceProvider = builder.Services.BuildServiceProvider();
var encryptionService = encryptionServiceProvider.GetRequiredService<IDatabaseEncryptionService>();
var encryptionKey = encryptionService.GetOrCreateEncryptionKey();

// Add database context with encrypted connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath};Password={encryptionKey}"));

// Add Identity services
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Configure password requirements
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Configure user options
    options.User.RequireUniqueEmail = true;

    // Configure sign-in options
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// Add authorization services
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Add authentication state provider for Blazor Server
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Register application services
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<IPersonService, PersonService>();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Initialize database and apply migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Apply any pending migrations
        context.Database.Migrate();

        // Ensure default configuration exists
        var configService = services.GetRequiredService<IConfigurationService>();
        await configService.EnsureDefaultConfigurationAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();