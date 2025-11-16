using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AdministratorWeb.Services;
using System.Threading.Tasks;

namespace AdministratorWeb.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class EmailTestController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailTestController> _logger;

        public EmailTestController(IEmailService emailService, ILogger<EmailTestController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendTest(string toEmail)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                TempData["Error"] = "Please enter an email address.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var htmlBody = @"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                    </head>
                    <body style='font-family: Arial, sans-serif; padding: 20px; background-color: #f4f4f4;'>
                        <div style='max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                            <h1 style='color: #4F46E5; margin-bottom: 20px;'>🎉 Email Test Successful!</h1>
                            <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                                Congratulations! Your SMTP email configuration is working correctly.
                            </p>
                            <div style='background-color: #f0fdf4; border-left: 4px solid #10B981; padding: 15px; margin: 20px 0;'>
                                <p style='color: #065F46; margin: 0;'>
                                    <strong>✓ Gmail SMTP Connected</strong><br>
                                    <strong>✓ MailKit Working</strong><br>
                                    <strong>✓ Email Notifications Ready</strong>
                                </p>
                            </div>
                            <p style='color: #666; font-size: 14px; margin-top: 20px;'>
                                Sent from: <strong>LuandRi Laundry Service</strong><br>
                                Time: " + DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt") + @"
                            </p>
                        </div>
                    </body>
                    </html>";

                var success = await _emailService.SendEmailAsync(
                    toEmail,
                    "Email Test",
                    "✅ SMTP Test - LuandRi Laundry Service",
                    htmlBody
                );

                if (success)
                {
                    _logger.LogInformation("Test email sent successfully to {Email}", toEmail);
                    TempData["Success"] = $"✅ Test email sent successfully to {toEmail}! Check your inbox (and spam folder).";
                }
                else
                {
                    _logger.LogWarning("Failed to send test email to {Email}", toEmail);
                    TempData["Error"] = "❌ Failed to send test email. Check logs for details.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test email to {Email}", toEmail);
                TempData["Error"] = $"❌ Error: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
