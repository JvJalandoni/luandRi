namespace AdministratorWeb.Models;

/// <summary>
/// Tracks all actions performed on laundry requests
/// </summary>
public class RequestActionLog
{
    public int Id { get; set; }

    /// <summary>
    /// Action performed (e.g., "AcceptRequest", "DeclineRequest", "CompleteRequest", etc.)
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Related laundry request ID
    /// </summary>
    public int RequestId { get; set; }

    /// <summary>
    /// Customer ID
    /// </summary>
    public required string CustomerId { get; set; }

    /// <summary>
    /// Customer name for easy display
    /// </summary>
    public required string CustomerName { get; set; }

    /// <summary>
    /// Request type (Pickup, Delivery, PickupAndDelivery)
    /// </summary>
    public string? RequestType { get; set; }

    /// <summary>
    /// Previous status (if applicable)
    /// </summary>
    public string? OldStatus { get; set; }

    /// <summary>
    /// New status (if applicable)
    /// </summary>
    public string? NewStatus { get; set; }

    /// <summary>
    /// Robot assigned to the request
    /// </summary>
    public string? AssignedRobotName { get; set; }

    /// <summary>
    /// Reason for action (e.g., decline reason)
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Weight of laundry in kg
    /// </summary>
    public decimal? WeightKg { get; set; }

    /// <summary>
    /// Total cost of the request
    /// </summary>
    public decimal? TotalCost { get; set; }

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
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// When the action was performed
    /// </summary>
    public DateTime ActionedAt { get; set; }

    /// <summary>
    /// Additional notes about the action
    /// </summary>
    public string? Notes { get; set; }
}
