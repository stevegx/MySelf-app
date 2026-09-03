using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MySelf.Api;
using MySelf.Api.Auth;
using MySelf.Api.Me;
using MySelf.Api.Nutrition;
using MySelf.Api.Workouts;
using MySelf.Infrastructure.Identity;
using MySelf.Infrastructure.Nutrition;
using MySelf.Infrastructure.Persistence;

// JwtSecurityTokenHandler otherwise silently remaps standard claim types (e.g. "sub") to
// long legacy Microsoft/SOAP claim URIs on the way in. Disabling that means the claims
// TokenService puts on the token (JwtRegisteredClaimNames.Sub, .Email) are exactly the
// claims MeEndpoints reads back — no hidden translation table in between.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// Load the repo-root .env into environment variables for local development.
// TraversePath() walks up from the working directory until it finds a .env file;
// NoClobber() keeps any real environment variable that is already set (e.g. in CI).
Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// RFC 7807 ProblemDetails for error responses (docs/04 API conventions).
builder.Services.AddProblemDetails();

// Rate limiting (docs/04: no rate limiting was a Phase 2 gap). Generous global ceiling plus
// stricter "auth" / "write" policies; disabled in the integration test environment.
builder.AddAppRateLimiting();
var rateLimitingEnabled = builder.Configuration.GetValue($"{RateLimitOptions.SectionName}:Enabled", true);

// Read from configuration key "ConnectionStrings:DefaultConnection". The .env line
// ConnectionStrings__DefaultConnection=... is mapped to that key by the environment
// variable configuration provider (double underscore => colon).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found. "
        + "Set ConnectionStrings__DefaultConnection in .env.");

builder.Services.AddDbContext<MySelfDbContext>(options =>
    options.UseNpgsql(connectionString));

// Health check that verifies the API can reach PostgreSQL through MySelfDbContext.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<MySelfDbContext>();

// --- Identity: user accounts + password hashing (docs/05) ---
// AddIdentityCore (not AddIdentity) registers UserManager and friends without also pulling
// in Identity's own cookie-based sign-in pipeline — this app issues its own JWT + refresh
// tokens (below) instead of using Identity's default cookie auth scheme.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        // Identity's UserName *is* the human-chosen, unique "username" (docs asked for one) —
        // this app doesn't set UserName to the email, so its own uniqueness/charset checks do
        // double duty as username validation for free. Default charset is alphanumeric + -._@+;
        // narrowed here to drop @ and + so a username can't be confused with an email address.
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-";

        // Lockout on repeated failed logins (docs/05: "lockout/rate limiting on auth
        // endpoints"). AllowedForNewUsers=true means it applies from account creation,
        // not just after an admin opts an account in — Identity's default, set explicitly
        // here so the policy is visible in one place rather than relying on the default.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<MySelfDbContext>()
    // Registers the token providers GeneratePasswordResetTokenAsync/ResetPasswordAsync
    // need (PasswordResetEndpoints) — without this they throw at runtime looking for a
    // provider named "Default" that was never registered.
    .AddDefaultTokenProviders();

// --- JWT access tokens + refresh tokens (docs/05) ---
// Same fail-fast pattern as the connection string: Jwt:Key is a secret and only ever comes
// from Jwt__Key in .env, never committed in appsettings.json.
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
if (string.IsNullOrEmpty(jwtSection["Key"]))
{
    throw new InvalidOperationException(
        "Jwt:Key was not found. Set Jwt__Key in .env.");
}

var jwtOptions = jwtSection.Get<JwtOptions>()!;
builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.AddSingleton<TokenService>();

// --- Auth middleware: validates the JWT on protected endpoints (docs/05) ---
// This only validates access tokens already issued by TokenService above — it has no
// knowledge of the refresh-token cookie; that flow is a separate slice (/auth/refresh).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            // Default is 5 minutes; tightened since these access tokens are short-lived
            // by design (AccessTokenMinutes), so a wide skew would defeat the point.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// --- CORS: only the frontend dev origin may call this API with credentials (docs/05) ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// --- Nutrition: Open Food Facts barcode lookup (docs/03 runtime integration + cache) ---
builder.Services
    .AddOptions<OpenFoodFactsOptions>()
    .Bind(builder.Configuration.GetSection(OpenFoodFactsOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<OpenFoodFactsClient>((sp, http) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenFoodFactsOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});
builder.Services.AddScoped<BarcodeLookupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

// Order matters: UseAuthentication figures out *who* the caller is (reads/validates the
// JWT into HttpContext.User); UseAuthorization then checks *whether* they're allowed to
// hit the endpoint (RequireAuthorization()). Both must come after UseCors and before the
// endpoints they protect.
app.UseAuthentication();
app.UseAuthorization();

// After authentication so the "write" policy can partition by the caller's user id.
if (rateLimitingEnabled)
{
    app.UseRateLimiter();
}

// Liveness/readiness probe. Deliberately unversioned (not under /api/v1, which is
// reserved for business resources).
app.MapHealthChecks("/health");

app.MapFoodsEndpoints();
app.MapAuthEndpoints();
app.MapPasswordResetEndpoints();
app.MapMeEndpoints();
app.MapNutritionEstimateEndpoints();
app.MapOnboardingEndpoints();
app.MapExerciseEndpoints();
app.MapProgramEndpoints();
app.MapWorkoutGroupEndpoints();
app.MapWorkoutVariantEndpoints();

app.Run();

// Exposes the implicit Program class to the integration test project
// (WebApplicationFactory<Program>).
public partial class Program;
