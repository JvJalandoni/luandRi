using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Payment methods supported by the laundry service
    /// </summary>
    public enum PaymentMethod
    {
        /// <summary>Payment via credit card</summary>
        CreditCard,
        /// <summary>Payment via debit card</summary>
        DebitCard,
        /// <summary>Payment via PayPal</summary>
        PayPal,
        /// <summary>Cash payment</summary>
        Cash,
        /// <summary>Direct bank transfer</summary>
        BankTransfer,
        /// <summary>Digital wallet (GCash, PayMaya, etc.)</summary>
        DigitalWallet
    }

    /// <summary>
    /// Status of a payment transaction
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>Payment created but not yet processed</summary>
        Pending,
        /// <summary>Payment successfully processed</summary>
        Completed,
        /// <summary>Payment processing failed</summary>
        Failed,
        /// <summary>Payment was refunded to customer</summary>
        Refunded,
        /// <summary>Payment was cancelled</summary>
        Cancelled
    }

    /// <summary>
    /// Represents a payment transaction for a laundry service request
    /// Tracks payment lifecycle including processing, refunds, and cancellations
    /// Auto-generates receipt upon successful completion
    /// </summary>
    public class Payment
    {
        /// <summary>Unique identifier for the payment</summary>
        public int Id { get; set; }

        /// <summary>ID of the associated laundry request</summary>
        [Required]
        public int LaundryRequestId { get; set; }

        /// <summary>Navigation property to associated laundry request</summary>
        public LaundryRequest LaundryRequest { get; set; } = null!;

        /// <summary>Customer's unique ID making the payment</summary>
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>Customer's full name</summary>
        [Required]
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>Payment amount in pesos (must be greater than 0)</summary>
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        /// <summary>Payment method used (Cash, Card, DigitalWallet, etc.)</summary>
        public PaymentMethod Method { get; set; } = PaymentMethod.CreditCard;

        /// <summary>Current status of the payment (Pending, Completed, Failed, etc.)</summary>
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        /// <summary>Transaction ID from payment gateway/processor</summary>
        public string? TransactionId { get; set; }

        /// <summary>Payment reference number provided by customer or system</summary>
        public string? PaymentReference { get; set; }

        /// <summary>Additional notes about the payment</summary>
        public string? Notes { get; set; }

        /// <summary>When the payment record was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the payment was processed/completed</summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>ID of admin user who processed the payment</summary>
        public string? ProcessedByUserId { get; set; }

        /// <summary>Navigation property to admin who processed payment</summary>
        public ApplicationUser? ProcessedByUser { get; set; }

        /// <summary>Reason why payment failed (if Status = Failed)</summary>
        public string? FailureReason { get; set; }

        // Refund tracking

        /// <summary>Amount refunded to customer (if Status = Refunded)</summary>
        public decimal? RefundAmount { get; set; }

        /// <summary>When the refund was issued</summary>
        public DateTime? RefundedAt { get; set; }

        /// <summary>ID of admin user who processed the refund</summary>
        public string? RefundedByUserId { get; set; }

        /// <summary>Reason for the refund</summary>
        public string? RefundReason { get; set; }

        // Cancellation tracking

        /// <summary>When the payment was cancelled</summary>
        public DateTime? CancelledAt { get; set; }

        /// <summary>ID of user who cancelled the payment</summary>
        public string? CancelledByUserId { get; set; }

        /// <summary>Reason for cancellation</summary>
        public string? CancellationReason { get; set; }
    }
}