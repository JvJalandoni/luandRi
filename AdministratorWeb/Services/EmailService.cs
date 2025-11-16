using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace AdministratorWeb.Services
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
        public bool EmailEnabled { get; set; } = true;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }

    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody, string textBody = "");
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody = "");
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody = "")
        {
            return await SendEmailAsync(toEmail, "", subject, htmlBody, textBody);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody, string textBody = "")
        {
            if (!_emailSettings.EmailEnabled)
            {
                _logger.LogWarning("Email sending is disabled in configuration. Email to {ToEmail} was not sent.", toEmail);
                return false;
            }

            if (string.IsNullOrWhiteSpace(textBody))
            {
                textBody = StripHtml(htmlBody);
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));

            if (!string.IsNullOrWhiteSpace(toName))
            {
                message.To.Add(new MailboxAddress(toName, toEmail));
            }
            else
            {
                message.To.Add(MailboxAddress.Parse(toEmail));
            }

            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody
            };

            message.Body = builder.ToMessageBody();

            int attempts = 0;
            int delay = _emailSettings.RetryDelayMilliseconds;

            while (attempts < _emailSettings.MaxRetryAttempts)
            {
                attempts++;
                try
                {
                    using (var client = new SmtpClient())
                    {
                        await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort,
                            _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                        if (!string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername))
                        {
                            await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                        }

                        await client.SendAsync(message);
                        await client.DisconnectAsync(true);

                        _logger.LogInformation("Email sent successfully to {ToEmail} with subject '{Subject}' on attempt {Attempt}",
                            toEmail, subject, attempts);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to {ToEmail} on attempt {Attempt}/{MaxAttempts}. Subject: {Subject}",
                        toEmail, attempts, _emailSettings.MaxRetryAttempts, subject);

                    if (attempts >= _emailSettings.MaxRetryAttempts)
                    {
                        _logger.LogError("All retry attempts exhausted for email to {ToEmail}. Email sending failed.", toEmail);
                        return false;
                    }

                    // Exponential backoff
                    await Task.Delay(delay);
                    delay *= 2;
                }
            }

            return false;
        }

        private string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            // Simple HTML stripping - removes tags but keeps content
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }
    }
}
