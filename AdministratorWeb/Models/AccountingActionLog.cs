namespace AdministratorWeb.Models;

/// <summary>
/// Tracks all actions performed on accounting/payment records
/// </summary>
public class AccountingActionLog
{
    public int Id { get; set; }

    /// <summary>
    /// Action performed (e.g., "MarkAsPaid", "MarkAsPending", "CancelPayment", etc.)
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Payment ID if action was on a payment
    /// </summary>
    public int? PaymentId { get; set; }

    /// <summary>
    /// Adjustment ID if action was on an adjustment
    /// </summary>
    public int? AdjustmentId { get; set; }

    /// <summary>
    /// Related laundry request ID
    /// </summary>
    public int? LaundryRequestId { get; set; }

    /// <summary>
    /// Customer ID (nullable for actions not tied to a specific customer)
    /// </summary>
    public string? CustomerId { get; set; }

    /// <summary>
    /// Customer name for easy display (nullable for actions not tied to a specific customer)
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// Amount involved in the action
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Previous status (if applicable)
    /// </summary>
    public string? OldStatus { get; set; }

    /// <summary>
    /// New status (if applicable)
    /// </summary>
    public string? NewStatus { get; set; }

    /// <summary>
    /// User who performed the action
    /// </summary>
    public string? PerformedByUserId { get; set; }

    /// <summary>
    /// User name for display
    /// </summary>
    public string? PerformedByUserName { get; set; }

    /// <summary>
    /// User email for display
    /// </summary>
    public string? PerformedByUserEmail { get; set; }

    /// <summary>
    /// Additional details about the action
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When the action was performed
    /// </summary>
    public DateTime ActionedAt { get; set; }

    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }
}
