using System;
using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Audit log for profile updates (both users and admins)
    /// </summary>
    public class ProfileUpdateLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// User ID who was updated
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Full name of user who was updated
        /// </summary>
        [Required]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Email of user who was updated (at time of update)
        /// </summary>
        public string? UserEmail { get; set; }

        /// <summary>
        /// User ID who made the update (null if self-update)
        /// </summary>
        public string? UpdatedByUserId { get; set; }

        /// <summary>
        /// Full name of user who made the update
        /// </summary>
        public string? UpdatedByUserName { get; set; }

        /// <summary>
        /// Email of user who made the update
        /// </summary>
        public string? UpdatedByUserEmail { get; set; }

        /// <summary>
        /// Update source: "Web", "MobileApp", "Admin"
        /// </summary>
        [Required]
        public string UpdateSource { get; set; } = string.Empty;

        /// <summary>
        /// JSON of old values before update
        /// </summary>
        public string? OldValues { get; set; }

        /// <summary>
        /// JSON of new values after update
        /// </summary>
        public string? NewValues { get; set; }

        /// <summary>
        /// Was password changed?
        /// </summary>
        public bool PasswordChanged { get; set; }

        /// <summary>
        /// Was profile picture changed?
        /// </summary>
        public bool ProfilePictureChanged { get; set; }

        /// <summary>
        /// IP address of requester
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// When the update occurred
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional notes or context
        /// </summary>
        public string? Notes { get; set; }
    }
}
