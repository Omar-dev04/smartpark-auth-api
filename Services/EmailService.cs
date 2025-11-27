using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace demoApi.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public void SendEmail(string to, string subject, string body)
        {
            try
            {
                var emailSettings = _config.GetSection("EmailSettings");
                var from = emailSettings["From"];
                var password = emailSettings["Password"];
                var host = emailSettings["Host"];
                var port = int.Parse(emailSettings["Port"]);

                using var client = new SmtpClient(host, port)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(from, password),
                    EnableSsl = true
                };

                var mail = new MailMessage(from, to, subject, body) { IsBodyHtml = true };
                client.Send(mail);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email Error: {ex.Message}"); 
            }
        }

    }
}
