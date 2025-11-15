using System;
using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Audit log for accounting and payment actions (mark paid, pending, failed, cancel, adjustments)
    /// </summary>
    public class AccountingActionLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Action performed: "MarkPaid", "MarkPending", "MarkFailed", "Cancel", "CreateAdjustment", "DeleteAdjustment"
        /// </summary>
        [Required]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Payment ID if action is related to payment
        /// </summary>
        public int? PaymentId { get; set; }

        /// <summary>
        /// Laundry request ID if action is related to request
        /// </summary>
        public int? LaundryRequestId { get; set; }

        /// <summary>
        /// Adjustment ID if action is related to adjustment
        /// </summary>
        public int? AdjustmentId { get; set; }

        /// <summary>
        /// Customer ID associated with the payment/request
        /// </summary>
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>
        /// Customer name at time of action
        /// </summary>
        [Required]
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Amount involved in the transaction
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Payment/adjustment status before action
        /// </summary>
        public string? OldStatus { get; set; }

        /// <summary>
        /// Payment/adjustment status after action
        /// </summary>
        public string? NewStatus { get; set; }

        /// <summary>
        /// Payment method (Cash, GCash, Card, etc.)
        /// </summary>
        public string? PaymentMethod { get; set; }

        /// <summary>
        /// Adjustment type (AddRevenue, SubtractRevenue, CompletePayment, SupplyExpense)
        /// </summary>
        public string? AdjustmentType { get; set; }

        /// <summary>
        /// Reason for failure/cancellation/adjustment
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Admin user ID who performed the action
        /// </summary>
        public string? PerformedByUserId { get; set; }

        /// <summary>
        /// Admin user name who performed the action
        /// </summary>
        public string? PerformedByUserName { get; set; }

        /// <summary>
        /// Admin user email who performed the action
        /// </summary>
        public string? PerformedByUserEmail { get; set; }

        /// <summary>
        /// IP address of the admin performing the action
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// When the action occurred
        /// </summary>
        [Required]
        public DateTime ActionedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional notes or context
        /// </summary>
        public string? Notes { get; set; }
    }
}
