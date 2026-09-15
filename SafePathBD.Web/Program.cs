using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Data;
using SafePathBD.Web.Integrations.Geocoding;
using SafePathBD.Web.Integrations.Routing;
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
builder.Services.AddMemoryCache();

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
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReportModerationService, ReportModerationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IRouteIncidentService, RouteIncidentService>();
builder.Services.AddScoped<IRoutingService, RoutingService>();

// Chunk 7 intelligent mobility. Dataset/model/transit catalogs are immutable during a
// process lifetime, while route/safety services remain request-scoped because they use EF Core.
builder.Services.AddSingleton<ITrafficDataRepository, TrafficDataRepository>();
builder.Services.AddSingleton<ITransitNetworkService, TransitNetworkService>();
builder.Services.AddSingleton<ITrafficPredictionService, TrafficPredictionService>();
builder.Services.AddSingleton<IRickshawRoadEligibilityPolicy, RickshawRoadEligibilityPolicy>();
builder.Services.AddScoped<IVehicleTravelTimeService, VehicleTravelTimeService>();
builder.Services.AddScoped<ISegmentSafetyProvider, SegmentSafetyProvider>();
builder.Services.AddScoped<IMultimodalGraphBuilder, MultimodalGraphBuilder>();
builder.Services.AddScoped<ITimeDependentJourneyRouter, TimeDependentDijkstraRouter>();
builder.Services.AddScoped<IKBestJourneyPlanner, KBestJourneyPlanner>();
builder.Services.AddScoped<IRouteResilienceService, RouteResilienceService>();
builder.Services.AddScoped<IRouteExplanationService, RouteExplanationService>();
builder.Services.AddScoped<IJourneyAssembler, JourneyAssembler>();
builder.Services.AddScoped<IDepartureWindowService, DepartureWindowService>();
builder.Services.AddScoped<IIntelligentRoutingService, IntelligentRoutingService>();

builder.Services.Configure<NominatimOptions>(builder.Configuration.GetSection(NominatimOptions.SectionName));

var routingProvider = builder.Configuration["Routing:Provider"] ?? "OSRM";
if (!string.Equals(routingProvider, "OSRM", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException($"Routing:Provider '{routingProvider}' is not supported by this build. Configure OSRM or register another IRoutingProvider implementation.");
}

builder.Services.AddOptions<OsrmRoutingOptions>()
    .Bind(builder.Configuration.GetSection(OsrmRoutingOptions.SectionName))
    .Validate(options =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
        "Routing:OSRM:BaseUrl must be an absolute HTTP/HTTPS URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Profile), "Routing:OSRM:Profile is required.")
    .Validate(options => options.TimeoutSeconds is >= 2 and <= 60, "Routing timeout must be between 2 and 60 seconds.")
    .Validate(options => options.CacheMinutes is >= 1 and <= 120, "Route cache must be between 1 and 120 minutes.")
    .Validate(options => options.IncidentRecheckSeconds is >= 30 and <= 300, "Incident recheck must be between 30 and 300 seconds.")
    .Validate(options => options.IncidentProximityMeters is >= 10 and <= 250, "Incident proximity must be between 10 and 250 metres.")
    .Validate(options => options.DetourOffsetMeters is >= 100 and <= 3000, "Detour offset must be between 100 and 3000 metres.")
    .Validate(options => options.MaxDetourAttempts is >= 1 and <= 8, "Max detour attempts must be between 1 and 8.")
    .Validate(options => options.MaxDetourDistanceFactor is >= 1.05 and <= 3.0, "Max detour distance factor must be between 1.05 and 3.0.")
    .Validate(options => options.CautionAlternativeDistanceFactor is >= 1.0 and <= 2.0, "Caution alternative distance factor must be between 1.0 and 2.0.")
    .ValidateOnStart();

builder.Services.AddOptions<SafePathBD.Web.Common.IntelligentMobilityOptions>()
    .Bind(builder.Configuration.GetSection(SafePathBD.Web.Common.IntelligentMobilityOptions.SectionName))
    .Validate(o => o.TrafficPredictionCacheMinutes is >= 1 and <= 240, "Traffic prediction cache must be between 1 and 240 minutes.")
    .Validate(o => o.TrafficRoadMatchMaxMeters is >= 100 and <= 5000, "Traffic road match threshold must be between 100 and 5000 metres.")
    .Validate(o => o.GraphSampleMeters is >= 50 and <= 1000, "Graph sample distance must be between 50 and 1000 metres.")
    .Validate(o => o.BusStopMatchMeters is >= 100 and <= 1500, "Bus stop match threshold must be between 100 and 1500 metres.")
    .Validate(o => o.CandidatePoolSize is >= 3 and <= 30, "Candidate pool must be between 3 and 30.")
    .Validate(o => o.TopJourneyCount is >= 1 and <= 5, "Top journey count must be between 1 and 5.")
    .Validate(o => o.DiversityThreshold is >= 0.5 and <= 1.0, "Diversity threshold must be between 0.5 and 1.0.")
    .Validate(o => o.BackupDiversityThreshold is >= 0.4 and <= 1.0, "Backup diversity threshold must be between 0.4 and 1.0.")
    .Validate(o => o.DefaultMaxTransfers is >= 0 and <= 2, "Default transfers must be between 0 and 2.")
    .Validate(o => o.DefaultMaxWalkingMeters >= 100 && o.DefaultMaxWalkingMeters <= o.MaximumWalkingMeters, "Default walking distance must be positive and not exceed the configured maximum.")
    .Validate(o => o.MaximumWalkingMeters is >= 500 and <= 5000, "Maximum walking distance must be between 500 and 5000 metres.")
    .Validate(o => o.GenericTransferMinutes is >= 0 and <= 10, "Generic transfer time must be between 0 and 10 minutes.")
    .Validate(o => o.BusTransferMinutes is >= 0 and <= 10, "Bus boarding/transfer time must be between 0 and 10 minutes.")
    .Validate(o => o.BusStopDwellMinutes is >= 0 and <= 5, "Bus stop dwell must be between 0 and 5 minutes.")
    .Validate(o => o.DepartureBenefitMinutes is >= 0 and <= 60, "Departure-window significance must be between 0 and 60 minutes.")
    .Validate(o => o.DepartureBenefitPercent is >= 0 and <= 1.0, "Departure-window percentage threshold must be between 0 and 1.")
    .Validate(o => o.IntelligentSearchCacheMinutes is >= 1 and <= 120, "Intelligent-search cache must be between 1 and 120 minutes.")
    .ValidateOnStart();

// Nominatim requires a descriptive User-Agent; it is configuration, not a secret.
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(NominatimGeocodingService.HttpClientName,
    (provider, client) =>
    {
        var options = provider.GetRequiredService<IOptions<NominatimOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    });

// OSRM is mediated by the backend so clients cannot choose arbitrary provider URLs.
builder.Services.AddHttpClient<IRoutingProvider, OsrmRoutingProvider>(OsrmRoutingProvider.HttpClientName,
    (provider, client) =>
    {
        var options = provider.GetRequiredService<IOptions<OsrmRoutingOptions>>().Value;
        var baseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal) ? options.BaseUrl : options.BaseUrl + "/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SafePathBD/1.0");
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

// Low-risk baseline response headers. A strict CSP is intentionally deferred until all
// existing inline script/style uses can be removed without breaking the established UI.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";
    await next();
});

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
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
