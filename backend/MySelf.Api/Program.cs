using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using MySelf.Api.Nutrition;
using MySelf.Infrastructure.Nutrition;
using MySelf.Infrastructure.Persistence;

// Load the repo-root .env into environment variables for local development.
// TraversePath() walks up from the working directory until it finds a .env file;
// NoClobber() keeps any real environment variable that is already set (e.g. in CI).
Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// RFC 7807 ProblemDetails for error responses (docs/04 API conventions).
builder.Services.AddProblemDetails();

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

// Liveness/readiness probe. Deliberately unversioned (not under /api/v1, which is
// reserved for business resources).
app.MapHealthChecks("/health");

app.MapFoodsEndpoints();

app.Run();

// Exposes the implicit Program class to the integration test project
// (WebApplicationFactory<Program>).
public partial class Program;
