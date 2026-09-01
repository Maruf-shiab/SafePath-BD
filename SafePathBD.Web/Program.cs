using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Data;
using SafePathBD.Web.Integrations.Geocoding;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Implementations;
using SafePathBD.Web.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set it with: " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Server=localhost;Port=3306;Database=safepath_bd;User=root;Password=<password>;\" --project SafePathBD.Web");
}

// Pinned so startup does not depend on probing the server for its version.
var serverVersion = new MySqlServerVersion(new Version(8, 0, 46));

builder.Services.AddDbContext<SafePathDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SafePathBD.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Development also serves plain HTTP on localhost; every other environment must be HTTPS-only.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher<Users>, PasswordHasher<Users>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmergencyService, EmergencyService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAccidentReportService, AccidentReportService>();
builder.Services.AddScoped<IHazardReportService, HazardReportService>();
builder.Services.AddScoped<IReportImageService, ReportImageService>();
builder.Services.AddScoped<IReportCommunityService, ReportCommunityService>();
builder.Services.AddScoped<IReportModerationService, ReportModerationService>();

builder.Services.Configure<NominatimOptions>(builder.Configuration.GetSection(NominatimOptions.SectionName));

// Nominatim requires a descriptive User-Agent; it is configuration, not a secret.
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(NominatimGeocodingService.HttpClientName,
    (provider, client) =>
    {
        var options = provider.GetRequiredService<IOptions<NominatimOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    });

// Add services to the container.
builder.Services.AddControllersWithViews();

// The community endpoints post JSON, so the antiforgery token travels in a header.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

await DatabaseConnectionCheck.VerifyAsync(app.Services, app.Logger);

app.Run();
