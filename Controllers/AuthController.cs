using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using BCrypt.Net;
using demoApi.Data;
using demoApi.Models;
using demoApi.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace demoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public AuthController(AppDbContext context, IConfiguration configuration, EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        // ------------------------------------------------ REGISTER ------------------------------------------------
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            if (req == null) return BadRequest("Body is empty!");

            if (_context.Users.Any(u => u.Email == req.Email))
                return BadRequest("Email already registered.");

            if (string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Password is required.");

            if (string.IsNullOrWhiteSpace(req.PhoneNumber))
                return BadRequest("Phone number is required.");

            if (!Regex.IsMatch(req.PhoneNumber, @"^(\+?\d{1,3}[- ]?)?\d{10}$"))
                return BadRequest("Invalid phone number format.");

            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(req.Email, emailRegex))
                return BadRequest("Invalid email format.");

            string hashed = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12);

            var user = new User
            {
                FullName = req.FullName,
                Email = req.Email,
                Password = hashed,
                PhoneNumber = req.PhoneNumber
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            // Send welcome email safely
            try
            {
                var subject = "🎉 Welcome to Smart Sayes!";
                var body = $@"
            <div style='font-family:Segoe UI,Arial;padding:35px;background:#f4f7fb;border-radius:10px'>
                <h2 style='color:#173C65'>Welcome {user.FullName} 🚗</h2>
                <p>Your account is now active. Login using <b>{user.Email}</b></p>
            </div>";

                _emailService.SendEmail(user.Email, subject, body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📩 Email send failed: {ex.Message}");
                // Do not block registration
            }

            return Ok("User registered successfully.");
        }


        // ------------------------------------------------ LOGIN ------------------------------------------------
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            if (req == null) return BadRequest("Body empty. Must send JSON.");
            if (string.IsNullOrWhiteSpace(req.Email)) return BadRequest("Email required.");
            if (string.IsNullOrWhiteSpace(req.Password)) return BadRequest("Password required.");

            try
            {
                var existingUser = _context.Users.FirstOrDefault(u => u.Email == req.Email);

                if (existingUser == null)
                    return Unauthorized("Invalid email or password.");

                if (string.IsNullOrEmpty(existingUser.Password))
                    return Unauthorized("Account registered with Google login only.");

                // Safe BCrypt verification
                bool verified = false;
                try
                {
                    verified = BCrypt.Net.BCrypt.Verify(req.Password, existingUser.Password);
                }
                catch
                {
                    // In case hash is invalid
                    return Unauthorized("Invalid email or password.");
                }

                if (!verified)
                    return Unauthorized("Invalid email or password.");

                var token = GenerateJwtToken(existingUser);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Login error: {ex.Message}");
                return StatusCode(500, "Internal server error during login.");
            }
        }

        // ------------------------------------------------ GOOGLE LOGIN ------------------------------------------------
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            if (request == null) return BadRequest("Empty body.");

            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
                var user = _context.Users.FirstOrDefault(u => u.Email == payload.Email);

                bool isNewUser = false;

                if (user == null)
                {
                    isNewUser = true;
                    user = new User { FullName = payload.Name, Email = payload.Email, Password = null };
                    _context.Users.Add(user);
                    _context.SaveChanges();
                }

                var jwt = GenerateJwtToken(user);
                return Ok(new { Token = jwt });
            }
            catch (Exception ex)
            {
                return BadRequest($"Invalid Google token: {ex.Message}");
            }
        }

        // ------------------------------------------------ CONFIG CHECK (NEW) ------------------------------------------------
        [HttpGet("config-check")]
        public IActionResult ConfigCheck()
        {
            return Ok(new
            {
                JwtIssuer = _configuration["JwtConfig:Issuer"],
                JwtAudience = _configuration["JwtConfig:Audience"],
                KeyLength = _configuration["JwtConfig:Key"]?.Length,
                EmailFrom = _configuration["EmailSettings:From"],
                EmailHost = _configuration["EmailSettings:Host"],
                EmailSSL = _configuration["EmailSettings:EnableSsl"]
            });
        }

        // ------------------------------------------------ TOKEN ------------------------------------------------
        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtConfig:Key"]));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtConfig:Issuer"],
                audience: _configuration["JwtConfig:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class GoogleLoginRequest { public string IdToken { get; set; } }
    }
}
