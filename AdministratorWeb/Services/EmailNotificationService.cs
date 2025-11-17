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
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(
            IEmailService emailService,
            ApplicationDbContext context,
            ILogger<EmailNotificationService> logger)
        {
            _emailService = emailService;
            _context = context;
            _logger = logger;
        }

        private string GetEmailHeader() => @"<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8"">
<title>LuandRi</title>
</head>
<body style=""margin:0;padding:0;font-family:Arial,sans-serif;background-color:#ffffff;"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
<tr>
<td align=""center"" style=""padding:48px 24px;"">
<table width=""560"" cellpadding=""0"" cellspacing=""0"" border=""0"">
<tr>
<td style=""padding-bottom:32px;"">
<span style=""font-size:18px;font-weight:bold;color:#111827;"">LuandRi</span>
</td>
</tr>
<tr>
<td>";

        private string GetEmailFooter(string year) => $@"</td>
</tr>
<tr>
<td style=""padding-top:48px;"">
<p style=""margin:0 0 4px 0;font-size:14px;color:#9ca3af;"">LuandRi Laundry Service</p>
<p style=""margin:0;font-size:12px;color:#9ca3af;"">&copy; {year} LuandRi</p>
</td>
</tr>
</table>
</td>
</tr>
</table>
</body>
</html>";

        public async Task SendEmailChangeOTPAsync(string userId, string email, string userName, string otpCode)
        {
            try
            {
                _logger.LogInformation("SendEmailChangeOTPAsync: user={UserId}, email={Email}, OTP={OtpCode}", userId, email, otpCode);
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Confirm your email change</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">You requested to change your email address. Use this code to verify:</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;text-align:center;margin:0 0 24px 0;"">
<span style=""font-size:28px;font-weight:bold;letter-spacing:6px;color:#111827;font-family:monospace;"">{otpCode}</span>
</div>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">This code expires in 15 minutes.</p>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">If you didn't request this change, you can ignore this email.</p>
<div style=""border-top:1px solid #e5e7eb;padding-top:24px;"">
<p style=""margin:0;font-size:14px;color:#6b7280;""><strong style=""color:#374151;"">Security tip:</strong> Never share this code with anyone.</p>
</div>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Email Verification Code\n\nHi {userName},\n\nYour verification code is: {otpCode}\n\nThis code expires in 15 minutes.\n\n---\nLuandRi Laundry Service";

                await _emailService.SendEmailAsync(email, userName, "Email Verification Code - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();
                var date = DateTime.Now.ToString("MMMM dd, yyyy");

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Payment Received</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">We have received your payment. Thank you!</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Amount: <strong style=""color:#111827;"">₱{amount:N2}</strong></p>
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Payment Method: <strong style=""color:#111827;"">{paymentMethod}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Date: <strong style=""color:#111827;"">{date}</strong></p>
</div>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">You can view your receipt in the mobile app.</p>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Payment Received\n\nHi {userName},\n\nWe received your payment.\nRequest ID: #{requestId}\nAmount: P{amount:N2}\nMethod: {paymentMethod}\nDate: {date}\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Payment Received - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();
                var date = DateTime.Now.ToString("MMMM dd, yyyy");

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Refund Issued</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">A refund has been issued for your request.</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Refund Amount: <strong style=""color:#111827;"">₱{amount:N2}</strong></p>
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Reason: <strong style=""color:#111827;"">{reason}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Date: <strong style=""color:#111827;"">{date}</strong></p>
</div>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Refund Issued\n\nHi {userName},\n\nRefund issued for Request #{requestId}\nAmount: P{amount:N2}\nReason: {reason}\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Refund Issued - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Request Accepted</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Your laundry request has been accepted!</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Assigned Robot: <strong style=""color:#111827;"">{robotName}</strong></p>
</div>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">Track your request in the mobile app.</p>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Request Accepted\n\nHi {userName},\n\nRequest #{requestId} accepted.\nRobot: {robotName}\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Request Accepted - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Request Declined</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Unfortunately, your laundry request has been declined.</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Reason: <strong style=""color:#111827;"">{reason}</strong></p>
</div>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Request Declined\n\nHi {userName},\n\nRequest #{requestId} declined.\nReason: {reason}\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Request Declined - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Laundry Completed</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Great news! Your laundry has been completed.</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Processed by: <strong style=""color:#111827;"">{robotName}</strong></p>
</div>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">Your laundry will be delivered soon.</p>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Laundry Completed\n\nHi {userName},\n\nRequest #{requestId} completed by {robotName}.\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Laundry Completed - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Delivery Started</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Your laundry is on its way!</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Delivered by: <strong style=""color:#111827;"">{robotName}</strong></p>
</div>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">Track delivery in the mobile app.</p>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Delivery Started\n\nHi {userName},\n\nRequest #{requestId} is being delivered by {robotName}.\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Delivery Started - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Delivery Completed</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Your laundry has been delivered!</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Delivered by: <strong style=""color:#111827;"">{robotName}</strong></p>
</div>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">Thank you for using LuandRi!</p>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Delivery Completed\n\nHi {userName},\n\nRequest #{requestId} delivered by {robotName}.\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Delivery Completed - LuandRi", htmlBody, textBody);
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
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Welcome to LuandRi!</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Thank you for joining LuandRi Laundry Service. We're excited to have you!</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">With LuandRi, you can:</p>
<ul style=""margin:0 0 24px 0;padding-left:20px;font-size:16px;color:#374151;"">
<li>Request laundry pickup from your room</li>
<li>Track your laundry in real-time</li>
<li>View payment history and receipts</li>
</ul>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">Download our mobile app to get started!</p>
" + GetEmailFooter(year);

                var textBody = $"Welcome to LuandRi!\n\nHi {userName},\n\nThank you for joining LuandRi Laundry Service.\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(email, userName, "Welcome to LuandRi Laundry Service", htmlBody, textBody);
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

                var year = DateTime.Now.Year.ToString();
                var date = DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt");

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Password Changed</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Your password was successfully changed on {date}.</p>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">If you didn't make this change, please contact us immediately.</p>
<div style=""border-top:1px solid #e5e7eb;padding-top:24px;"">
<p style=""margin:0;font-size:14px;color:#6b7280;""><strong style=""color:#374151;"">Security tip:</strong> Use a strong, unique password.</p>
</div>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Password Changed\n\nHi {userName},\n\nYour password was changed on {date}.\n\nIf you didn't do this, contact us immediately.\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Password Changed - LuandRi", htmlBody, textBody);
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                var userName = $"{user.FirstName} {user.LastName}";
                var year = DateTime.Now.Year.ToString();

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">Payment Reminder</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">This is a friendly reminder that you have an outstanding payment.</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0 0 8px 0;font-size:14px;color:#6b7280;"">Request ID: <strong style=""color:#111827;"">#{requestId}</strong></p>
<p style=""margin:0;font-size:14px;color:#6b7280;"">Amount Due: <strong style=""color:#111827;"">₱{amount:N2}</strong></p>
</div>
<p style=""margin:0 0 32px 0;font-size:16px;color:#6b7280;"">Please complete your payment at your earliest convenience.</p>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Payment Reminder\n\nHi {userName},\n\nRequest #{requestId} has outstanding payment of P{amount:N2}.\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(user.Email!, userName, "Payment Reminder - LuandRi", htmlBody, textBody);
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
                var year = DateTime.Now.Year.ToString();
                var sentTime = DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt");

                var htmlBody = GetEmailHeader() + $@"
<h1 style=""margin:0 0 24px 0;font-size:24px;font-weight:bold;color:#111827;"">New Message from {adminName}</h1>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">Hi {userName},</p>
<p style=""margin:0 0 24px 0;font-size:16px;color:#374151;"">You have received a new message:</p>
<div style=""background-color:#f3f4f6;padding:20px;border-radius:8px;margin:0 0 24px 0;"">
<p style=""margin:0;font-size:16px;color:#111827;"">{messageContent}</p>
</div>
<p style=""margin:0 0 32px 0;font-size:14px;color:#6b7280;"">Sent on {sentTime}</p>
" + GetEmailFooter(year);

                var textBody = $"LuandRi - Message from {adminName}\n\nHi {userName},\n\nMessage:\n{messageContent}\n\nSent: {sentTime}\n\n---\nLuandRi";

                await _emailService.SendEmailAsync(email, userName, $"New Message from {adminName} - LuandRi", htmlBody, textBody);
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
