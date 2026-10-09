using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JSWMI_CRM.Models
{
    [Table("sap_sync_logs")]
    public class SapSyncLog
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        [Column("reference_id")]
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [Required]
        [Column("reference_type")]
        [JsonPropertyName("reference_type")]
        public string ReferenceType { get; set; } = "CuttingPlan"; // CuttingPlan, SalesOrder

        [Required]
        [Column("endpoint")]
        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        [Column("http_method")]
        [JsonPropertyName("http_method")]
        public string HttpMethod { get; set; } = "POST";

        [Column("request_payload")]
        [JsonPropertyName("request_payload")]
        public string? RequestPayload { get; set; }

        [Column("response_payload")]
        [JsonPropertyName("response_payload")]
        public string? ResponsePayload { get; set; }

        [Column("http_status_code")]
        [JsonPropertyName("http_status_code")]
        public int HttpStatusCode { get; set; }

        [Column("is_success")]
        [JsonPropertyName("is_success")]
        public bool IsSuccess { get; set; }

        [Column("error_message")]
        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
