using DashLane.Data;
using DashLane.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

EnvFile.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
Text.Load(Path.Combine(Directory.GetCurrentDirectory(), "Resources"));

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

var googleOptions = builder.Configuration.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>()
    ?? new GoogleAuthOptions();
builder.Services.AddSingleton(googleOptions);

var otpOptions = OtpOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(otpOptions);
if (!otpOptions.IsConfigured)
{
    Console.WriteLine("Warning: SMS codes are not configured. Add TWILIO_ACCOUNT_SID and TWILIO_AUTH_TOKEN to .env.");
}

builder.Services.AddSingleton(builder.Configuration.GetSection(SiteOptions.SectionName).Get<SiteOptions>() ?? new SiteOptions());

var idCheckOptions = builder.Configuration.GetSection(IdCheckOptions.SectionName).Get<IdCheckOptions>() ?? new IdCheckOptions();
builder.Services.AddSingleton(idCheckOptions);
if (!idCheckOptions.IsConfigured)
{
    Console.WriteLine("Note: automatic ID checks are off. Add IdCheck__IdAnalyzerApiKey to .env (free plan at https://www.idanalyzer.com).");
}

builder.Services.AddHttpClient("twilio", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("idanalyzer", client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient("nominatim", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DashLane/1.0 (local ride-sharing app)");
    client.Timeout = TimeSpan.FromSeconds(8);
});

builder.Services.AddDbContext<DashLaneContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DashLane")));
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<OtpSender>();
builder.Services.AddScoped<IdVerifier>();
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "en-CA", "fr-CA", "en-US" };
    options.SetDefaultCulture("en-CA")
        .AddSupportedCultures(cultures)
        .AddSupportedUICultures(cultures);
    options.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
});

var authentication = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/SignIn";
        options.LogoutPath = "/Home/SignOut";
        options.Cookie.Name = "dashlane.auth";
        options.Cookie.HttpOnly = true;
        options.SlidingExpiration = true;
    })
    .AddCookie("External", options =>
    {
        options.Cookie.Name = "dashlane.external";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.Cookie.HttpOnly = true;
    });

if (googleOptions.IsConfigured)
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = googleOptions.ClientId;
        options.ClientSecret = googleOptions.ClientSecret;
        options.SignInScheme = "External";
        options.CallbackPath = "/signin-google";
        options.SaveTokens = false;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
}

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DashLaneContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.Run();
