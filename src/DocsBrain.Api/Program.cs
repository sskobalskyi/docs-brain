using DocsBrain.Api.Data;
using DocsBrain.Api.Documents;
using DocsBrain.Api.Tenancy;
using DocsBrain.Core.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

builder.Services.AddDbContext<DocsBrainDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres")));

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

app.MapHealthChecks("/health");
app.MapDocumentEndpoints();

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
