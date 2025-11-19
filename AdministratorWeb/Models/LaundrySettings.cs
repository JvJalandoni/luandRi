using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Room detection mode - determines how robots identify rooms
    /// </summary>
    public enum RoomDetectionMode
    {
        /// <summary>
        /// Use Bluetooth beacons for room identification
        /// </summary>
        Beacon = 0,

        /// <summary>
        /// Use floor color detection for room identification
        /// </summary>
        Color = 1
    }

    /// <summary>
    /// Global system settings for laundry service configuration
    /// Stored as single row in database (singleton pattern)
    /// Controls pricing, company info, robot behavior, and room detection mode
    /// </summary>
    public class LaundrySettings
    {
        /// <summary>Primary key (always 1 for singleton)</summary>
        public int Id { get; set; }

        /// <summary>Price per kilogram of laundry in pesos (must be > 0)</summary>
        [Required]
        [Display(Name = "Rate per Kg")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Rate must be greater than 0")]
        public decimal RatePerKg { get; set; } = 10.00m;

        /// <summary>Company name displayed on receipts and UI</summary>
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "Autonomous Laundry Service";

        /// <summary>Company address for receipts and contact info</summary>
        [Display(Name = "Company Address")]
        public string? CompanyAddress { get; set; }

        /// <summary>Company phone number for customer support</summary>
        [Display(Name = "Company Phone")]
        public string? CompanyPhone { get; set; }

        /// <summary>Company email for customer inquiries</summary>
        [Display(Name = "Company Email")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string? CompanyEmail { get; set; }

        /// <summary>Company website URL</summary>
        [Display(Name = "Company Website")]
        public string? CompanyWebsite { get; set; }

        /// <summary>Brief description of company and services</summary>
        [Display(Name = "Company Description")]
        public string? CompanyDescription { get; set; }

        /// <summary>Facebook page URL for social media links</summary>
        [Display(Name = "Facebook URL")]
        public string? FacebookUrl { get; set; }

        /// <summary>Twitter profile URL for social media links</summary>
        [Display(Name = "Twitter URL")]
        public string? TwitterUrl { get; set; }

        /// <summary>Instagram profile URL for social media links</summary>
        [Display(Name = "Instagram URL")]
        public string? InstagramUrl { get; set; }

        /// <summary>Service operating hours (e.g., "8:00 AM - 6:00 PM")</summary>
        [Display(Name = "Operating Hours")]
        public string? OperatingHours { get; set; } = "8:00 AM - 6:00 PM";

        /// <summary>Maximum weight allowed per request in kilograms (default 50kg)</summary>
        [Display(Name = "Maximum Weight per Request (kg)")]
        public decimal? MaxWeightPerRequest { get; set; } = 50.0m;

        /// <summary>Minimum weight required per request in kilograms (default 1kg)</summary>
        [Display(Name = "Minimum Weight per Request (kg)")]
        public decimal? MinWeightPerRequest { get; set; } = 1.0m;

        /// <summary>Whether to automatically accept new requests without admin approval</summary>
        [Display(Name = "Auto Accept Requests")]
        public bool AutoAcceptRequests { get; set; } = false;

        /// <summary>Room detection method: Beacon (Bluetooth) or Color (floor color matching)</summary>
        [Display(Name = "Room Detection Mode")]
        public RoomDetectionMode DetectionMode { get; set; } = RoomDetectionMode.Beacon;

        /// <summary>Red component (0-255) of line color for robot to follow (default 0 = black)</summary>
        [Display(Name = "Line Follow Color (Red)")]
        [Range(0, 255)]
        public byte LineFollowColorR { get; set; } = 0;

        /// <summary>Green component (0-255) of line color for robot to follow (default 0 = black)</summary>
        [Display(Name = "Line Follow Color (Green)")]
        [Range(0, 255)]
        public byte LineFollowColorG { get; set; } = 0;

        /// <summary>Blue component (0-255) of line color for robot to follow (default 0 = black)</summary>
        [Display(Name = "Line Follow Color (Blue)")]
        [Range(0, 255)]
        public byte LineFollowColorB { get; set; } = 0;

        /// <summary>Maximum time in minutes robot has to reach room before timeout (1-60, default 5)</summary>
        [Display(Name = "Room Arrival Timeout (minutes)")]
        [Range(1, 60, ErrorMessage = "Timeout must be between 1 and 60 minutes")]
        public int RoomArrivalTimeoutMinutes { get; set; } = 5;

        /// <summary>Maximum number of requests a user can make per calendar day (null = unlimited, default 10)</summary>
        [Display(Name = "Max Requests Per User Per Day")]
        [Range(1, 100, ErrorMessage = "Must be between 1 and 100 requests per day")]
        public int? MaxRequestsPerDay { get; set; } = 10;

        /// <summary>External download URL for customer mobile app (MediaFire, Google Drive, etc.)</summary>
        [Display(Name = "APK Download URL")]
        [Url(ErrorMessage = "Please enter a valid URL")]
        public string? ApkDownloadUrl { get; set; }

        /// <summary>Last time these settings were updated</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}