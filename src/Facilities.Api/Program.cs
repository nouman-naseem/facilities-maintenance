using System.Text;
using Facilities.Api.Middleware;
using Facilities.Api.Security;
using Facilities.Core.Abstractions;
using Facilities.Core.Services;
using Facilities.Infrastructure.Persistence;
using Facilities.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured. See README.md.");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured. See README.md.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<MaintenanceRequestService>();
builder.Services.AddScoped<SpendReportService>();
builder.Services.AddScoped<TokenService>();

builder.Services
    .AddAuthentication(options =>
    {
        // Razor Pages (browser) authenticate via cookie by default; the JSON API
        // under /api explicitly requires the Bearer scheme (see ApiControllerBase).
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApproverOnly", policy => policy.RequireRole("Approver"));
});

builder.Services.AddControllers();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Requests");
    options.Conventions.AuthorizeFolder("/Reports", "ApproverOnly");
});
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations and seed demo data on startup so the app is usable immediately
// after `docker compose up` + `dotnet run` — see README.md. Uses its own DbContext
// instance (not the DI-resolved one) since there is no HTTP request/tenant yet.
{
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);
    await using var startupDb = new AppDbContext(optionsBuilder.Options, new AnonymousCurrentUserContext());
    await startupDb.Database.MigrateAsync();
    await DbSeeder.SeedAsync(startupDb, new PasswordHasherService());
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
