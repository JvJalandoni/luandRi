using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministratorWeb.Models;

/// <summary>
/// Audit log entry for tracking all administrative actions in the system
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Primary key - auto-incrementing ID
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Type of action performed
    /// </summary>
    [Required]
    public AuditActionType ActionType { get; set; }

    /// <summary>
    /// Human-readable description of the action
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string ActionDescription { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity affected (e.g., "LaundryRequest", "ApplicationUser", "Room")
    /// </summary>
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the affected entity
    /// </summary>
    [MaxLength(50)]
    public string? EntityId { get; set; }

    /// <summary>
    /// Name or description of the entity for quick reference
    /// </summary>
    [MaxLength(200)]
    public string? EntityName { get; set; }

    /// <summary>
    /// ID of the user who performed the action
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Username of the user who performed the action
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Email of the user who performed the action
    /// </summary>
    [MaxLength(256)]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// When the action occurred (UTC)
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address of the user (IPv4 or IPv6)
    /// </summary>
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from the HTTP request
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Session ID for tracking user sessions
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }

    /// <summary>
    /// JSON-serialized old values before the change
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON-serialized new values after the change
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Comma-separated list of fields that changed
    /// </summary>
    [MaxLength(500)]
    public string? ChangedFields { get; set; }

    /// <summary>
    /// HTTP request path (e.g., "/Requests/Accept")
    /// </summary>
    [MaxLength(500)]
    public string? RequestPath { get; set; }

    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE)
    /// </summary>
    [MaxLength(10)]
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Additional context information as JSON
    /// </summary>
    public string? AdditionalInfo { get; set; }

    /// <summary>
    /// Whether the action completed successfully
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Error message if the action failed
    /// </summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Navigation property to the user who performed the action
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
}

/// <summary>
/// Enum for different types of audit actions
/// </summary>
public enum AuditActionType
{
    // Authentication & Authorization
    Login = 1,
    Logout = 2,
    LoginFailed = 3,

    // Laundry Request Management
    RequestCreated = 100,
    RequestAccepted = 101,
    RequestDeclined = 102,
    RequestCompleted = 103,
    RequestCancelled = 104,
    RequestStatusChanged = 105,
    RobotAssigned = 106,
    RobotReassigned = 107,
    ManualRequestCreated = 108,
    RequestWeightUpdated = 109,
    RequestMarkedForPickup = 110,
    RequestMarkedForDelivery = 111,
    DeliveryStarted = 112,

    // Robot Management
    RobotConnected = 200,
    RobotDisconnected = 201,
    RobotStatusChanged = 202,
    RobotCommandSent = 203,

    // User/Customer Management
    UserCreated = 300,
    UserUpdated = 301,
    UserDeleted = 302,
    UserActivated = 303,
    UserDeactivated = 304,
    UserRoleChanged = 305,
    BeaconAssigned = 306,
    RoomAssignmentChanged = 307,

    // Room Management
    RoomCreated = 400,
    RoomUpdated = 401,
    RoomDeleted = 402,
    RoomActivated = 403,
    RoomDeactivated = 404,

    // Beacon Management
    BeaconCreated = 500,
    BeaconUpdated = 501,
    BeaconDeleted = 502,
    BeaconStatusChanged = 503,

    // Payment & Accounting
    PaymentCreated = 600,
    PaymentProcessed = 601,
    PaymentRefunded = 602,
    PaymentCancelled = 603,
    PaymentAdjustmentCreated = 604,
    ReceiptGenerated = 605,

    // System Settings
    SettingsUpdated = 700,
    PriceChanged = 701,
    CompanyInfoUpdated = 702,
    SystemConfigChanged = 703,
    DetectionModeChanged = 704,

    // Messages
    MessageSent = 800,
    MessageRead = 801,
    MessageDeleted = 802,

    // System Actions
    BulkAction = 900,
    DataExported = 901,
    ReportGenerated = 902,
    SystemMaintenance = 903,
    ForceCancelAll = 904
}
