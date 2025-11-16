using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministratorWeb.Models
{
    public enum EmailStatus
    {
        Pending,
        Sent,
        Failed
    }

    [Table("EmailQueue")]
    public class EmailQueue
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string ToEmail { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ToName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "text")]
        public string HtmlBody { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string TextBody { get; set; } = string.Empty;

        [Required]
        public EmailStatus Status { get; set; } = EmailStatus.Pending;

        public int Attempts { get; set; } = 0;

        public DateTime? LastAttemptAt { get; set; }

        public DateTime? SentAt { get; set; }

        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
