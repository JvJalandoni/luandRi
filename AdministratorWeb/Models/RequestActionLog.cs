using System;
using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Audit log for laundry request actions (accept, decline, complete, cancel, manual creation, etc.)
    /// </summary>
    public class RequestActionLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Laundry request ID that was acted upon
        /// </summary>
        [Required]
        public int RequestId { get; set; }

        /// <summary>
        /// Customer ID associated with the request
        /// </summary>
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>
        /// Customer name at time of action
        /// </summary>
        [Required]
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Action performed: "Accept", "Decline", "Complete", "Cancel", "ManualCreate", "ForceCancelAll", "MarkForPickup", "StartDelivery"
        /// </summary>
        [Required]
        public string Action { get; set; } = string.Empty;

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
        /// Request status before action
        /// </summary>
        public string? OldStatus { get; set; }

        /// <summary>
        /// Request status after action
        /// </summary>
        public string? NewStatus { get; set; }

        /// <summary>
        /// Assigned robot name (if applicable)
        /// </summary>
        public string? AssignedRobotName { get; set; }

        /// <summary>
        /// Decline/cancel reason (if applicable)
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Request type for manual creation (RobotDelivery, WalkIn)
        /// </summary>
        public string? RequestType { get; set; }

        /// <summary>
        /// Weight in kg (for manual walk-in requests)
        /// </summary>
        public decimal? WeightKg { get; set; }

        /// <summary>
        /// Total cost calculated (if applicable)
        /// </summary>
        public decimal? TotalCost { get; set; }

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
