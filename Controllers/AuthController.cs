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

        // ------------------- REGISTER -------------------


        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            if (_context.Users.Any(u => u.Email == req.Email))
                return BadRequest("Email already registered.");

            if (string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Password is required.");

            if (string.IsNullOrWhiteSpace(req.PhoneNumber))
                return BadRequest("Phone number is required.");
            if (!string.IsNullOrEmpty(req.PhoneNumber))
            {
                if (!Regex.IsMatch(req.PhoneNumber, @"^(\+?\d{1,3}[- ]?)?\d{10}$"))
                {
                    return BadRequest("Invalid phone number format.");
                }
            }
            //Email format validation using regex
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(req.Email, emailRegex))
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

            // ------------------- Send Welcome Email -------------------
            var subject = "🎉 Welcome to Smart Sayes!";
            var body = $@"
                <div style='font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:35px;border-radius:10px;'>
                  <div style='background:#173C65;padding:25px;border-radius:10px 10px 0 0;text-align:center;'>
                    <h1 style='color:#fff;margin:0;font-size:28px;'>Welcome to Smart Sayes 🚗</h1>
                  </div>

                  <div style='background:#fff;padding:30px;border-radius:0 0 10px 10px;box-shadow:0 4px 10px rgba(0,0,0,0.08)'>
                    <p style='color:#506C99;font-size:16px;margin-bottom:25px;'>
                      Hi <b>{user.FullName}</b>,<br><br>
                      We’re thrilled to have you join <b>Smart Sayes</b> — your smart companion for parking management, automation, and convenience.
                    </p>
                    <p style='color:#506C99;font-size:14px;line-height:1.6;margin-top:10px'>
                      Your account is now active. You can sign in anytime using your email: <b>{user.Email}</b>.
                    </p>

                    <p style='color:#D8AF17;font-size:13px;text-align:center;margin-top:40px'>
                      Need help? Contact us at 
                      <a href='mailto:smartsayessystem@gmail.com' style='color:#173C65;text-decoration:none;'>smartsayessystem@gmail.com</a>
                    </p>
                  </div>
                </div>";

            //_emailService.SendEmail(user.Email, subject, body);

            return Ok("User registered successfully.");
        }
        // ------------------- LOGIN -------------------
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            var existingUser = _context.Users.FirstOrDefault(u => u.Email == req.Email);

            if (existingUser == null)
                return Unauthorized("Invalid email or password.");

            if (string.IsNullOrEmpty(existingUser.Password))
                return Unauthorized("This account uses external login (Google).");

            bool verified = false;

            if (existingUser.Password.StartsWith("$2a$") ||
                existingUser.Password.StartsWith("$2b$") ||
                existingUser.Password.StartsWith("$2y$"))
            {
                verified = BCrypt.Net.BCrypt.Verify(req.Password ?? string.Empty, existingUser.Password);
            }
            else if (existingUser.Password == req.Password)
            {
                verified = true;
                existingUser.Password = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12);
                _context.Users.Update(existingUser);
                _context.SaveChanges();
            }

            if (!verified)
                return Unauthorized("Invalid email or password.");

            var token = GenerateJwtToken(existingUser);
            return Ok(new { Token = token });
        }


        // ------------------- GOOGLE LOGIN -------------------
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
                var user = _context.Users.FirstOrDefault(u => u.Email == payload.Email);

                bool isNewUser = false;

                if (user == null)
                {
                    isNewUser = true;
                    user = new User
                    {
                        FullName = payload.Name,
                        Email = payload.Email,
                        Password = null
                    };
                    _context.Users.Add(user);
                    _context.SaveChanges();
                }

                // Send welcome email only for new Google signups
                if (isNewUser)
                {
                    var subject = "🎉 Welcome to Smart Sayes!";
                    var body = $@"
                        <div style='font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:35px;border-radius:10px;'>
                          <div style='background:#173C65;padding:25px;border-radius:10px 10px 0 0;text-align:center;'>
                            <h1 style='color:#fff;margin:0;font-size:28px;'>Welcome to Smart Sayes 🚗</h1>
                          </div>

                          <div style='background:#fff;padding:30px;border-radius:0 0 10px 10px;box-shadow:0 4px 10px rgba(0,0,0,0.08)'>
                            <p style='color:#506C99;font-size:16px;margin-bottom:25px;'>
                              Hi <b>{user.FullName}</b>,<br><br>
                              Thanks for signing in with Google! Your Smart Sayes account has been created successfully.
                            </p>

                            <div style='text-align:center;margin:35px 0;'>
                              <a href='https://smartsayes.com' 
                                 style='background:#F6DD55;color:#173C65;padding:12px 28px;
                                 border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;
                                 box-shadow:0 2px 6px rgba(0,0,0,0.1);'>
                                 Get Started
                              </a>
                            </div>

                            <p style='color:#506C99;font-size:14px;line-height:1.6;margin-top:10px'>
                              Enjoy your smart experience with automated parking and instant notifications.
                            </p>

                            <p style='color:#D8AF17;font-size:13px;text-align:center;margin-top:40px'>
                              Need help? Contact us at 
                              <a href='mailto:smartsayessystem@gmail.com' style='color:#173C65;text-decoration:none;'>smartsayessystem@gmail.com</a>
                            </p>
                          </div>
                        </div>";

                    _emailService.SendEmail(user.Email, subject, body);
                }

                var jwt = GenerateJwtToken(user);
                return Ok(new { Token = jwt });
            }
            catch (Exception ex)
            {
                return BadRequest($"Invalid Google token: {ex.Message}");
            }
        }
        // ------------------- Forget Password -------------------
        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == req.Email);
            if (user == null)
                return NotFound("Email not registered.");

            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            user.Password = user.Password + $"|{token}";
            _context.Users.Update(user);
            _context.SaveChanges();

            var resetLink = $"https://localhost:7236/reset-password.html?email={user.Email}&token={token}";

            var subject = "🔐 Reset Your Smart Sayes Password";
            var body = $@"
                <div style='font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:35px;border-radius:10px;'>
                  <div style='background:#173C65;padding:25px;border-radius:10px 10px 0 0;text-align:center;'>
                    <h1 style='color:#fff;margin:0;font-size:26px;'>Reset Your Password</h1>
                  </div>

                  <div style='background:#fff;padding:30px;border-radius:0 0 10px 10px;box-shadow:0 4px 10px rgba(0,0,0,0.08)'>
                    <p style='color:#506C99;font-size:16px;margin-bottom:25px;'>
                      Hi <b>{user.FullName}</b>,<br><br>
                      We received a request to reset your password for your Smart Sayes account. 
                      If you didn’t make this request, you can safely ignore this email.
                    </p>

                    <div style='text-align:center;margin:35px 0;'>
                      <a href='{resetLink}' 
                         style='background:#F6DD55;color:#173C65;padding:12px 28px;
                         border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;
                         box-shadow:0 2px 6px rgba(0,0,0,0.1);'>
                         Reset Password
                      </a>
                    </div>

                    <p style='color:#506C99;font-size:14px;line-height:1.6;margin-top:10px'>
                      This link will expire in 15 minutes for your security.
                      Once opened, you’ll be able to create a new password immediately.
                    </p>

                    <p style='color:#D8AF17;font-size:13px;text-align:center;margin-top:40px'>
                      Need help? Reach us at 
                      <a href='mailto:smartsayessystem@gmail.com' style='color:#173C65;text-decoration:none;'>smartsayessystem@gmail.com</a>
                    </p>
                  </div>
                </div>";

            _emailService.SendEmail(req.Email, subject, body);
            return Ok("Reset link sent to your email.");
        }

        public class ForgotPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        public class ResetPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
        // ------------------- Reset Password -------------------
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest req)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == req.Email);
            if (user == null)
                return NotFound("User not found.");

            var decodedToken = Uri.UnescapeDataString(req.Token);
            if (!user.Password.Contains(decodedToken))
                return BadRequest("Invalid or expired token.");

            var newHashed = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 12);
            user.Password = newHashed;
            _context.Users.Update(user);
            _context.SaveChanges();

            return Ok("Password reset successful!");
        }
        // ------------------- JWT TOKEN GENERATION -------------------
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
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // Model for Google login request
    public class GoogleLoginRequest
    {
        public string IdToken { get; set; }
    }
}
