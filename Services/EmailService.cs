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
            var emailSettings = _config.GetSection("EmailSettings");
            var from = emailSettings["From"];
            var password = emailSettings["Password"];
            var host = emailSettings["Host"];
            var port = int.Parse(emailSettings["Port"]);

            using (var client = new SmtpClient(host, port))
            {
                client.UseDefaultCredentials = false; 
                client.Credentials = new NetworkCredential(from, password);
                client.EnableSsl = true; 
                client.DeliveryMethod = SmtpDeliveryMethod.Network; 

                var mail = new MailMessage();
                mail.From = new MailAddress(from, "Smart Parking System");
                mail.To.Add(to);
                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true;

                client.Send(mail);
            }
        }
    }
}
