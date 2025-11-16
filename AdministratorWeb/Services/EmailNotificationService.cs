using AdministratorWeb.Data;
using AdministratorWeb.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AdministratorWeb.Services
{
    public interface IEmailNotificationService
    {
        Task SendEmailChangeOTPAsync(string userId, string email, string userName, string otpCode);
        Task SendPaymentCompletedAsync(string userId, int requestId, decimal amount, string paymentMethod, string? adminName = null);
        Task SendRefundIssuedAsync(string userId, int requestId, decimal amount, string reason, string? adminName = null);
        Task SendRequestAcceptedAsync(string userId, int requestId, string robotName, string? adminName = null);
        Task SendRequestDeclinedAsync(string userId, int requestId, string reason, string? adminName = null);
        Task SendRequestCompletedAsync(string userId, int requestId, string robotName);
        Task SendDeliveryStartedAsync(string userId, int requestId, string robotName);
        Task SendDeliveryCompletedAsync(string userId, int requestId, string robotName);
        Task SendWelcomeEmailAsync(string userId, string userName, string email);
        Task SendPasswordChangedAsync(string userId, string userName);
        Task SendPaymentPendingAsync(string userId, int requestId, decimal amount);
        Task SendAdminMessageAsync(string userId, string userName, string email, string adminName, string messageContent);
    }

    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _templateService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly IWebHostEnvironment _env;

        public EmailNotificationService(
            IEmailService emailService,
            IEmailTemplateService templateService,
            ApplicationDbContext context,
            ILogger<EmailNotificationService> logger,
            IWebHostEnvironment env)
        {
            _emailService = emailService;
            _templateService = templateService;
            _context = context;
            _logger = logger;
            _env = env;
        }

        private async Task<bool> CheckUserPreferencesAsync(string userId, string notificationType)
        {
            var preferences = await _context.EmailPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (preferences == null || !preferences.EmailNotificationsEnabled)
                return false;

            return notificationType switch
            {
                "payment" => preferences.PaymentNotifications,
                "request" => preferences.RequestStatusNotifications,
                "security" => preferences.SecurityNotifications,
                _ => true
            };
        }

        private string LoadTemplate(string templateName)
        {
            var templatePath = Path.Combine(_env.ContentRootPath, "Views", "EmailTemplates", $"{templateName}.html");
            if (!File.Exists(templatePath))
            {
                _logger.LogError("Email template not found: {TemplatePath}", templatePath);
                return string.Empty;
            }
            return File.ReadAllText(templatePath);
        }

        private Dictionary<string, string> GetBaseVariables()
        {
            return new Dictionary<string, string>
            {
                { "companyName", "LuandRi Laundry Service" },
                { "supportEmail", "luandricorp@gmail.com" },
                { "date", DateTime.Now.ToString("MMMM dd, yyyy") },
                { "time", DateTime.Now.ToString("hh:mm tt") }
            };
        }

        public async Task SendEmailChangeOTPAsync(string userId, string email, string userName, string otpCode)
        {
            try
            {
                var template = LoadTemplate("email_change_otp");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", userName);
                variables.Add("email", email);
                variables.Add("otpCode", otpCode);

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(email, userName, "Email Verification Code - LuandRi", htmlBody);

                await LogEmailAsync(userId, "EmailChangeOTP", email, "Email Verification Code");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email change OTP to {Email}", email);
            }
        }

        public async Task SendPaymentCompletedAsync(string userId, int requestId, decimal amount, string paymentMethod, string? adminName = null)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "payment")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("payment_completed");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("amount", amount.ToString("N2"));
                variables.Add("paymentMethod", paymentMethod);
                variables.Add("adminName", adminName ?? "Admin");

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Payment Received - LuandRi", htmlBody);

                await LogEmailAsync(userId, "PaymentCompleted", user.Email!, "Payment Received");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment completed email for request {RequestId}", requestId);
            }
        }

        public async Task SendRefundIssuedAsync(string userId, int requestId, decimal amount, string reason, string? adminName = null)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "payment")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("refund_issued");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("amount", amount.ToString("N2"));
                variables.Add("reason", reason);
                variables.Add("adminName", adminName ?? "Admin");

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Refund Issued - LuandRi", htmlBody);

                await LogEmailAsync(userId, "RefundIssued", user.Email!, "Refund Issued");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send refund issued email for request {RequestId}", requestId);
            }
        }

        public async Task SendRequestAcceptedAsync(string userId, int requestId, string robotName, string? adminName = null)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "request")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("request_accepted");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("robotName", robotName);
                variables.Add("adminName", adminName ?? "Admin");

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Request Accepted - LuandRi", htmlBody);

                await LogEmailAsync(userId, "RequestAccepted", user.Email!, "Request Accepted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send request accepted email for request {RequestId}", requestId);
            }
        }

        public async Task SendRequestDeclinedAsync(string userId, int requestId, string reason, string? adminName = null)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "request")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("request_declined");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("reason", reason);
                variables.Add("adminName", adminName ?? "Admin");

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Request Declined - LuandRi", htmlBody);

                await LogEmailAsync(userId, "RequestDeclined", user.Email!, "Request Declined");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send request declined email for request {RequestId}", requestId);
            }
        }

        public async Task SendRequestCompletedAsync(string userId, int requestId, string robotName)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "request")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("request_completed");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("robotName", robotName);

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Laundry Completed - LuandRi", htmlBody);

                await LogEmailAsync(userId, "RequestCompleted", user.Email!, "Laundry Completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send request completed email for request {RequestId}", requestId);
            }
        }

        public async Task SendDeliveryStartedAsync(string userId, int requestId, string robotName)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "request")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("delivery_started");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("robotName", robotName);

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Delivery Started - LuandRi", htmlBody);

                await LogEmailAsync(userId, "DeliveryStarted", user.Email!, "Delivery Started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send delivery started email for request {RequestId}", requestId);
            }
        }

        public async Task SendDeliveryCompletedAsync(string userId, int requestId, string robotName)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "request")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("delivery_completed");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("robotName", robotName);

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Delivery Completed - LuandRi", htmlBody);

                await LogEmailAsync(userId, "DeliveryCompleted", user.Email!, "Delivery Completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send delivery completed email for request {RequestId}", requestId);
            }
        }

        public async Task SendWelcomeEmailAsync(string userId, string userName, string email)
        {
            try
            {
                var template = LoadTemplate("welcome");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", userName);
                variables.Add("email", email);

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(email, userName, "Welcome to LuandRi Laundry Service", htmlBody);

                await LogEmailAsync(userId, "Welcome", email, "Welcome to LuandRi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
            }
        }

        public async Task SendPasswordChangedAsync(string userId, string userName)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("password_changed");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", userName);

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, userName, "Password Changed - LuandRi", htmlBody);

                await LogEmailAsync(userId, "PasswordChanged", user.Email!, "Password Changed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password changed email to user {UserId}", userId);
            }
        }

        public async Task SendPaymentPendingAsync(string userId, int requestId, decimal amount)
        {
            try
            {
                if (!await CheckUserPreferencesAsync(userId, "payment")) return;

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var template = LoadTemplate("payment_pending");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", $"{user.FirstName} {user.LastName}");
                variables.Add("requestId", requestId.ToString());
                variables.Add("amount", amount.ToString("N2"));

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                    "Payment Reminder - LuandRi", htmlBody);

                await LogEmailAsync(userId, "PaymentPending", user.Email!, "Payment Reminder");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment pending email for request {RequestId}", requestId);
            }
        }

        public async Task SendAdminMessageAsync(string userId, string userName, string email, string adminName, string messageContent)
        {
            try
            {
                var template = LoadTemplate("admin_message");
                if (string.IsNullOrEmpty(template)) return;

                var variables = GetBaseVariables();
                variables.Add("userName", userName);
                variables.Add("adminName", adminName);
                variables.Add("messageContent", messageContent);
                variables.Add("sentTime", DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt"));
                variables.Add("currentYear", DateTime.Now.Year.ToString());

                var htmlBody = _templateService.RenderTemplate(template, variables);
                await _emailService.SendEmailAsync(email, userName, $"New Message from {adminName} - LuandRi", htmlBody);

                await LogEmailAsync(userId, "AdminMessage", email, $"Message from {adminName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin message email to {Email}", email);
            }
        }

        private async Task LogEmailAsync(string userId, string emailType, string toEmail, string subject)
        {
            try
            {
                var log = new EmailLog
                {
                    UserId = userId,
                    EmailType = emailType,
                    ToEmail = toEmail,
                    Subject = subject,
                    SentAt = DateTime.UtcNow,
                    Delivered = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.EmailLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log email send for type {EmailType}", emailType);
            }
        }
    }
}
