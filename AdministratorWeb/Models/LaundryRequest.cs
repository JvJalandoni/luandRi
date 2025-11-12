using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Status of a laundry request through its complete lifecycle
    /// Covers pickup, washing, payment, and delivery stages
    /// </summary>
    public enum RequestStatus
    {
        /// <summary>Request created, awaiting admin acceptance</summary>
        Pending,
        /// <summary>Admin accepted request, robot will be dispatched soon</summary>
        Accepted,
        /// <summary>Request is being processed</summary>
        InProgress,
        /// <summary>Robot dispatched and navigating to customer room for pickup</summary>
        RobotEnRoute,
        /// <summary>Robot arrived at customer's room, awaiting laundry loading</summary>
        ArrivedAtRoom,
        /// <summary>Customer confirmed laundry loaded onto robot</summary>
        LaundryLoaded,
        /// <summary>Robot returned to base with laundry</summary>
        ReturnedToBase,
        /// <summary>Laundry weighed and cost calculated</summary>
        WeighingComplete,
        /// <summary>Awaiting customer payment confirmation</summary>
        PaymentPending,
        /// <summary>Request fully completed (paid and delivered/picked up)</summary>
        Completed,
        /// <summary>Admin declined the request</summary>
        Declined,
        /// <summary>Customer cancelled the request</summary>
        Cancelled,
        /// <summary>Laundry is being washed</summary>
        Washing,

        /// <summary>Robot arrived at room with clean laundry for delivery</summary>
        FinishedWashingArrivedAtRoom,
        /// <summary>Washing done, customer chose delivery, admin must load laundry on robot</summary>
        FinishedWashingReadyToDeliver,
        /// <summary>Robot en route to customer room with clean laundry</summary>
        FinishedWashingGoingToRoom,
        /// <summary>Robot returning to base after delivering clean laundry</summary>
        FinishedWashingGoingToBase,
        /// <summary>Laundry washed and ready for customer pickup at base</summary>
        FinishedWashingAwaitingPickup,
        /// <summary>Washing completed, awaiting delivery option selection</summary>
        FinishedWashing,
        /// <summary>Clean laundry at base, ready for pickup</summary>
        FinishedWashingAtBase
    }

    /// <summary>
    /// Type of service requested by customer
    /// </summary>
    public enum RequestType
    {
        /// <summary>Robot picks up dirty laundry only (customer picks up clean laundry later)</summary>
        Pickup,
        /// <summary>Robot delivers clean laundry only (laundry already at base)</summary>
        Delivery,
        /// <summary>Full service: robot picks up dirty laundry and delivers clean laundry</summary>
        PickupAndDelivery
    }

    /// <summary>
    /// Represents a laundry service request from a customer
    /// Tracks complete workflow from pickup through washing, payment, and delivery
    /// Integrated with robot navigation and beacon-based room detection
    /// </summary>
    public class LaundryRequest
    {
        /// <summary>Unique identifier for the request</summary>
        public int Id { get; set; }

        /// <summary>Customer's unique ID (from AspNetUsers)</summary>
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>Customer's full name</summary>
        [Required]
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>Customer's contact phone number</summary>
        [Required]
        public string CustomerPhone { get; set; } = string.Empty;

        /// <summary>Customer's room number or address (e.g., "Room 201")</summary>
        [Required]
        public string Address { get; set; } = string.Empty;

        /// <summary>Optional special instructions from customer (e.g., "Separate whites")</summary>
        public string? Instructions { get; set; }

        /// <summary>Type of service requested (Pickup, Delivery, or PickupAndDelivery)</summary>
        public RequestType Type { get; set; }

        /// <summary>Current status in the workflow (Pending, InProgress, Completed, etc.)</summary>
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        /// <summary>Weight of laundry in kilograms (measured after pickup)</summary>
        public decimal? Weight { get; set; }

        /// <summary>Indicates if the current weight exceeds the maximum allowed weight</summary>
        public bool IsWeightExceeded { get; set; } = false;

        /// <summary>Total cost in pesos calculated from weight × price per kg</summary>
        public decimal? TotalCost { get; set; }

        /// <summary>Whether customer has paid for the service</summary>
        public bool IsPaid { get; set; } = false;

        /// <summary>When the request was created by customer</summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the service is scheduled to start (optional)</summary>
        public DateTime? ScheduledAt { get; set; }

        /// <summary>When the entire request was completed</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>When admin began processing the request</summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>Name of robot assigned to handle this request (e.g., "RobotA")</summary>
        public string? AssignedRobotName { get; set; }

        /// <summary>ID of admin user handling this request</summary>
        public string? HandledById { get; set; }

        /// <summary>Navigation property to admin user handling request</summary>
        public ApplicationUser? HandledBy { get; set; }

        /// <summary>Reason provided by admin if request was declined</summary>
        public string? DeclineReason { get; set; }

        // Workflow timestamps

        /// <summary>When admin accepted the request</summary>
        public DateTime? AcceptedAt { get; set; }

        /// <summary>When robot was dispatched for pickup</summary>
        public DateTime? RobotDispatchedAt { get; set; }

        /// <summary>When robot arrived at customer's room</summary>
        public DateTime? ArrivedAtRoomAt { get; set; }

        /// <summary>When customer confirmed laundry was loaded onto robot</summary>
        public DateTime? LaundryLoadedAt { get; set; }

        /// <summary>When robot returned to base with laundry</summary>
        public DateTime? ReturnedToBaseAt { get; set; }

        /// <summary>When laundry was weighed and cost calculated</summary>
        public DateTime? WeighingCompletedAt { get; set; }

        /// <summary>When payment was requested from customer</summary>
        public DateTime? PaymentRequestedAt { get; set; }

        /// <summary>When customer completed payment</summary>
        public DateTime? PaymentCompletedAt { get; set; }

        // Beacon and room information

        /// <summary>MAC address of Bluetooth beacon assigned to customer's room</summary>
        public string? AssignedBeaconMacAddress { get; set; }

        /// <summary>Name/number of customer's room (e.g., "Room 201")</summary>
        public string? RoomName { get; set; }

        // Payment information

        /// <summary>Payment method used (Cash, Card, DigitalWallet, BankTransfer)</summary>
        public string? PaymentMethod { get; set; }

        /// <summary>Payment transaction reference number</summary>
        public string? PaymentReference { get; set; }

        /// <summary>Additional notes about payment</summary>
        public string? PaymentNotes { get; set; }

        // Pricing information

        /// <summary>Price per kilogram in pesos (default 25.00)</summary>
        public decimal PricePerKg { get; set; } = 25.00m;

        /// <summary>Minimum charge in pesos regardless of weight (default 50.00)</summary>
        public decimal MinimumCharge { get; set; } = 50.00m;
    }
}