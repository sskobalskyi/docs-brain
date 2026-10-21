using DocsBrain.Api.Data;
using DocsBrain.Api.Documents;
using DocsBrain.Api.Tenancy;
using System.Threading.RateLimiting;
using DocsBrain.Api.Ai;
using DocsBrain.Api.Chat;
using DocsBrain.Api.Ingestion;
using DocsBrain.Api.Search;
using DocsBrain.Core.Ingestion;
using DocsBrain.Core.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

builder.Services.AddSingleton<ITextExtractor, PlainTextExtractor>();
builder.Services.AddSingleton<ITextExtractor, PdfTextExtractor>();
builder.Services.AddSingleton<ITextExtractor, DocxTextExtractor>();
builder.Services.AddScoped<IngestionService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddAiProviders(builder.Configuration);
builder.Services.Configure<ChatOptions>(builder.Configuration.GetSection("Chat"));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection("Search"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<UsageStore>();
builder.Services.AddScoped<ChatService>();

// Chat is the only endpoint that spends LLM tokens, so it is limited per tenant.
builder.Services.AddRateLimiter(o => o.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
builder.Services.AddOptions<RateLimiterOptions>().Configure<IOptions<ChatOptions>>((limiter, chat) =>
    limiter.AddPolicy("chat", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst(HttpTenantContext.ClaimType)?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = chat.Value.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
        })));
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 10 * 1024 * 1024);

builder.Services.AddDbContext<DocsBrainDbContext>(o =>
    o.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres"),
        npgsql => npgsql.UseVector()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidateAudience = false,
        IssuerSigningKey = JwtSetup.SigningKey(jwt),
    });

// Every authenticated caller must carry a tenant claim.
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(HttpTenantContext.ClaimType)
        .Build());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapDocumentEndpoints();
app.MapChatEndpoints();
app.MapGet("/api/search", async (string q, int? limit, SearchService search, CancellationToken ct) =>
        await search.SearchAsync(q, Math.Clamp(limit ?? 5, 1, 20), ct))
    .RequireAuthorization();

if (app.Environment.IsDevelopment())
{
    // Local-only: issue a token for a seeded tenant (Acme or Globex).
    app.MapPost("/dev/token", (DevTokenRequest request) =>
        JwtSetup.IssueToken(jwt, request.TenantId, request.User ?? "demo"));

    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<DocsBrainDbContext>().Database.EnsureCreated();
}

app.Run();

public sealed record DevTokenRequest(Guid TenantId, string? User);

public partial class Program;
