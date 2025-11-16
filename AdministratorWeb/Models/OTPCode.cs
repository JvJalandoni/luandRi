using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministratorWeb.Models
{
    [Table("OTPCodes")]
    public class OTPCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(6)]
        public string Code { get; set; } = string.Empty;

        /// <summary>Purpose of OTP: EmailChange, PasswordReset, etc.</summary>
        [MaxLength(50)]
        public string? Purpose { get; set; }

        /// <summary>New email address when purpose is EmailChange</summary>
        [MaxLength(256)]
        public string? NewEmail { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool Verified { get; set; } = false;

        /// <summary>Alias for Verified to match usage in code</summary>
        [NotMapped]
        public bool IsUsed { get => Verified; set => Verified = value; }

        public DateTime? VerifiedAt { get; set; }

        /// <summary>Alias for VerifiedAt to match usage in code</summary>
        [NotMapped]
        public DateTime? UsedAt { get => VerifiedAt; set => VerifiedAt = value; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
