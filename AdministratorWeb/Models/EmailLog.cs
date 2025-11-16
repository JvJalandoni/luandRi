using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministratorWeb.Models
{
    [Table("EmailLogs")]
    public class EmailLog
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string EmailType { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string ToEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool Delivered { get; set; } = false;

        public bool? Opened { get; set; }

        public bool? ClickedLink { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
