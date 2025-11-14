using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Application user entity extending ASP.NET Core Identity
    /// Represents both admin staff and customers in the laundry system
    /// Customers have assigned Bluetooth beacons for robot room navigation
    /// Admins have HandledRequests collection for request management tracking
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>User's first name</summary>
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>User's last name</summary>
        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Computed full name combining first and last name</summary>
        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        /// <summary>When user account was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Last time user logged into the system</summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>Whether user account is active (inactive users cannot log in)</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Path to user's profile picture (e.g., "/uploads/profiles/userId.jpg")
        /// Null if no profile picture has been uploaded
        /// </summary>
        [Display(Name = "Profile Picture")]
        [StringLength(500)]
        public string? ProfilePicturePath { get; set; }

        /// <summary>
        /// MAC address of Bluetooth beacon assigned to customer's room (e.g., "AA:BB:CC:DD:EE:FF")
        /// Used by robot to detect arrival at customer's room during navigation
        /// Only applicable for customer accounts, null for admin accounts
        /// </summary>
        [Display(Name = "Assigned Beacon MAC")]
        [StringLength(17)]
        public string? AssignedBeaconMacAddress { get; set; }

        /// <summary>
        /// Room number or name where customer resides (e.g., "Room 201")
        /// Used for display purposes and address confirmation
        /// </summary>
        [Display(Name = "Assigned Room")]
        [StringLength(100)]
        public string? RoomName { get; set; }

        /// <summary>
        /// Additional room description (e.g., "2nd floor, east wing")
        /// Helps staff and robot navigation system locate the room
        /// </summary>
        [Display(Name = "Room Description")]
        [StringLength(200)]
        public string? RoomDescription { get; set; }

        /// <summary>
        /// Collection of laundry requests handled by this user (if admin)
        /// Empty for customer accounts
        /// </summary>
        public ICollection<LaundryRequest> HandledRequests { get; set; } = new List<LaundryRequest>();
    }
}