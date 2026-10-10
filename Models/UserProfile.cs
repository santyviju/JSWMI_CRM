using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JSWMI_CRM.Models
{
    [Table("user_profiles")]
    public class UserProfile
    {
        [Key]
        [Column("user_id")]
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [Column("full_name")]
        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("role")]
        [JsonPropertyName("role")]
        public string Role { get; set; } = "Planner"; // Admin, Planner, Supervisor

        [Column("location_id")]
        [JsonPropertyName("location_id")]
        public string? LocationId { get; set; }

        [Column("location_name")]
        [JsonPropertyName("location_name")]
        public string? LocationName { get; set; }

        [Column("phone_number")]
        [JsonPropertyName("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
