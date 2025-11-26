using System.Text;
using demoApi.Data;
using demoApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// FORCE RAILWAY PORT
// ------------------------
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// ------------------------
// Add Controllers & Swagger
// ------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ------------------------
// DATABASE: Convert Railway URL if needed
// ------------------------
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (databaseUrl != null && databaseUrl.StartsWith("postgres://"))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var npgsqlBuilder = new NpgsqlConnectionStringBuilder()
    {
        Host = uri.Host,
        Port = uri.Port,
        Username = userInfo[0],
        Password = userInfo[1],
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Require,
        TrustServerCertificate = true
    };
    databaseUrl = npgsqlBuilder.ToString();
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(databaseUrl)
);

// ------------------------
// Email Service
// ------------------------
builder.Services.AddScoped<EmailService>();

// ------------------------
// JWT Authentication
// ------------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["JwtConfig:Issuer"],
        ValidAudience = builder.Configuration["JwtConfig:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtConfig:Key"]!)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
    };
});

builder.Services.AddAuthorization();

// ------------------------
// Build App
// ------------------------
var app = builder.Build();

// ------------------------
// Swagger
// ------------------------
app.UseSwagger();
app.UseSwaggerUI();

// ------------------------
// Middleware
// ------------------------
app.UseAuthentication();
app.UseAuthorization();

// ------------------------
// Root endpoint for testing
// ------------------------
app.MapGet("/", () => "SmartPark Auth API is running");

// ------------------------
// Controllers
// ------------------------
app.MapControllers();

// ------------------------
// Auto-migrate DB on startup (optional)
// ------------------------
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        Console.WriteLine("Database migrated successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Database migration error: " + ex.Message);
    }
}

// ------------------------
// Run App
// ------------------------
app.Run();
