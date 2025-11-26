using System.Text;
using demoApi.Data;
using demoApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// FORCE RAILWAY PORT (required)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// Add controllers
builder.Services.AddControllers();

// Swagger (ALWAYS ENABLE — Railway cannot run IsDevelopment())
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);





// Email service
builder.Services.AddScoped<EmailService>();

// JWT
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

var app = builder.Build();

// Swagger ALWAYS ON
app.UseSwagger();
app.UseSwaggerUI();

// Railway does NOT support HTTPS redirection — REMOVE IT
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Root endpoint for testing
app.MapGet("/", () => "SmartPark Auth API is running");

// Controllers
app.MapControllers();

app.Run();
