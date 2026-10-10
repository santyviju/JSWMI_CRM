using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JSWMI_CRM.Models
{
    [Table("locations")]
    public class Location
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required(ErrorMessage = "Location Code is required (e.g. VIJ, BLR, MUM, PUN).")]
        [StringLength(20)]
        [Column("code")]
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location Name is required.")]
        [StringLength(150)]
        [Column("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        [Column("city")]
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [StringLength(100)]
        [Column("state")]
        [JsonPropertyName("state")]
        public string? State { get; set; }

        [Column("is_active")]
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
