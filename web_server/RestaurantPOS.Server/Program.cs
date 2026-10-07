using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Hubs;
using RestaurantPOS.Server.Middleware;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Structured Logging with Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/server-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Configure Tenancy and Database Contexts (Database-per-Tenant)
builder.Services.AddHttpContextAccessor();

var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";

// Master Catalog Database (stores tenant registrations)
builder.Services.AddDbContext<RestaurantPOS.Server.Tenancy.MasterDbContext>(options =>
{
    if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        var baseConn = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";
        var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder(baseConn) { Database = "restaurantpos_master" };
        options.UseNpgsql(npgsqlBuilder.ConnectionString);
    }
    else
    {
        Directory.CreateDirectory("tenants");
        options.UseSqlite("Data Source=tenants/master.db");
    }
});

// Multi-Tenant Services
builder.Services.AddScoped<RestaurantPOS.Server.Tenancy.ITenantProvider, RestaurantPOS.Server.Tenancy.TenantProvider>();
builder.Services.AddScoped<RestaurantPOS.Server.Tenancy.ITenantService, RestaurantPOS.Server.Tenancy.TenantService>();
builder.Services.AddScoped<RestaurantPOS.Server.Tenancy.ITenantNotifier, RestaurantPOS.Server.Tenancy.TenantNotifier>();
builder.Services.AddSingleton<RestaurantPOS.Server.Tenancy.IPosPresenceTracker, RestaurantPOS.Server.Tenancy.PosPresenceTracker>();

// Dynamic Store Database Context (Physically isolated 1 DB per Tenant)
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var tenantProvider = serviceProvider.GetRequiredService<RestaurantPOS.Server.Tenancy.ITenantProvider>();
    var tenantService = serviceProvider.GetRequiredService<RestaurantPOS.Server.Tenancy.ITenantService>();
    var connectionString = tenantService.GetTenantConnectionString(tenantProvider.CurrentTenantCode);

    if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// 3. Configure Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "RestaurantPOS_Default_Super_Secret_Key_For_Jwt_2026_LongerThan32Bytes!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "RestaurantPOS",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "RestaurantPOSClients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// 4. Configure Forwarded Headers (Cloudflare Tunnel / Reverse Proxy support)
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 5. Configure CORS (allow POS client and Web App)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 5. Add Controllers and SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

builder.Services.AddOpenApi();

var app = builder.Build();

// 6. Initialize Master Catalog & Default Store Database on startup
using (var scope = app.Services.CreateScope())
{
    var tenantService = scope.ServiceProvider.GetRequiredService<RestaurantPOS.Server.Tenancy.ITenantService>();
    await tenantService.EnsureMasterAndDefaultTenantAsync();
}

app.UseForwardedHeaders();

// 7. Global Exception Handling Middleware (MANDATORY Error Logger & Handler)
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Serve static web app files (Customer QR & Management SPA) if present in wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<PosHub>("/hubs/pos");

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new 
{ 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0",
    Server = "RestaurantPOS Linux/Windows Server"
}));

// Fallback to index.html for client-side routing
app.MapFallbackToFile("index.html");

Log.Information("[Startup] Restaurant POS Central Server starting on .NET 10...");
app.Run();
