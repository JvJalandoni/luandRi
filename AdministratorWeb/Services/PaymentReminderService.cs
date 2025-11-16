using AdministratorWeb.Data;
using AdministratorWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdministratorWeb.Services
{
    /// <summary>
    /// Background service that runs every 24 hours to send payment reminders
    /// for outstanding laundry requests
    /// </summary>
    public class PaymentReminderService : BackgroundService
    {
        private readonly ILogger<PaymentReminderService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

        public PaymentReminderService(
            ILogger<PaymentReminderService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Payment Reminder Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPaymentReminders(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Payment Reminder Service");
                }

                // Wait 24 hours before next check
                _logger.LogInformation("Payment Reminder Service sleeping for 24 hours. Next check at {NextCheck}",
                    DateTime.Now.Add(_checkInterval));
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Payment Reminder Service stopped");
        }

        private async Task ProcessPaymentReminders(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

            _logger.LogInformation("Starting payment reminder check at {Time}", DateTime.Now);

            // Get all laundry requests with outstanding payments
            // Status must be PaymentPending, FinishedWashing, or related statuses but payment not received
            var outstandingRequests = await context.LaundryRequests
                .Where(r =>
                    (r.Status == RequestStatus.PaymentPending ||
                     r.Status == RequestStatus.FinishedWashing ||
                     r.Status == RequestStatus.FinishedWashingAtBase ||
                     r.Status == RequestStatus.FinishedWashingAwaitingPickup) &&
                    !r.IsPaid &&
                    r.TotalCost > 0)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} requests with outstanding payments", outstandingRequests.Count);

            int emailsSent = 0;
            foreach (var request in outstandingRequests)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Check if we already sent a reminder today to avoid spam
                    var lastReminder = await context.EmailLogs
                        .Where(e =>
                            e.UserId == request.CustomerId &&
                            e.EmailType == "PaymentPending" &&
                            e.SentAt >= DateTime.UtcNow.AddHours(-24))
                        .OrderByDescending(e => e.SentAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (lastReminder != null)
                    {
                        _logger.LogInformation("Skipping request {RequestId} - reminder already sent in last 24 hours", request.Id);
                        continue;
                    }

                    // Send payment reminder email
                    await emailService.SendPaymentPendingAsync(
                        request.CustomerId,
                        request.Id,
                        request.TotalCost ?? 0
                    );

                    emailsSent++;
                    _logger.LogInformation("Sent payment reminder for request {RequestId} to customer {CustomerId}",
                        request.Id, request.CustomerId);

                    // Small delay to avoid overwhelming email server
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send payment reminder for request {RequestId}", request.Id);
                    // Continue with next request
                }
            }

            _logger.LogInformation("Payment reminder check completed. Sent {EmailsSent} reminder emails", emailsSent);
        }
    }
}
