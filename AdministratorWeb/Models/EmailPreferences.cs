using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministratorWeb.Models
{
    [Table("EmailPreferences")]
    public class EmailPreferences
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        public bool EmailNotificationsEnabled { get; set; } = true;

        public bool PaymentNotifications { get; set; } = true;

        public bool RequestStatusNotifications { get; set; } = true;

        public bool SecurityNotifications { get; set; } = true;

        public bool MarketingNotifications { get; set; } = false;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
