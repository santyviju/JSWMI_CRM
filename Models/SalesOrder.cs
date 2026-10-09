using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JSWMI_CRM.Models
{
    [Table("sales_orders")]
    public class SalesOrder
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        [Column("doc_num")]
        [JsonPropertyName("doc_num")]
        public string DocNum { get; set; } = string.Empty;

        [Column("doc_entry")]
        [JsonPropertyName("doc_entry")]
        public int? DocEntry { get; set; }

        [Required]
        [Column("card_code")]
        [JsonPropertyName("card_code")]
        public string CardCode { get; set; } = string.Empty;

        [Required]
        [Column("card_name")]
        [JsonPropertyName("card_name")]
        public string CardName { get; set; } = string.Empty;

        [Column("doc_date")]
        [JsonPropertyName("doc_date")]
        public DateTime DocDate { get; set; } = DateTime.Today;

        [Column("doc_due_date")]
        [JsonPropertyName("doc_due_date")]
        public DateTime DocDueDate { get; set; } = DateTime.Today.AddDays(7);

        [Column("doc_total")]
        [JsonPropertyName("doc_total")]
        public decimal DocTotal { get; set; }

        [Column("currency")]
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "INR";

        [Column("status")]
        [JsonPropertyName("status")]
        public string Status { get; set; } = "Open";

        [Column("remarks")]
        [JsonPropertyName("remarks")]
        public string? Remarks { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public List<SalesOrderItem> Items { get; set; } = new();
    }

    [Table("sales_order_items")]
    public class SalesOrderItem
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Column("sales_order_id")]
        [JsonPropertyName("sales_order_id")]
        public string SalesOrderId { get; set; } = string.Empty;

        [Column("line_num")]
        [JsonPropertyName("line_num")]
        public int LineNum { get; set; }

        [Column("item_code")]
        [JsonPropertyName("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Column("item_description")]
        [JsonPropertyName("item_description")]
        public string ItemDescription { get; set; } = string.Empty;

        [Column("quantity")]
        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        [Column("uom")]
        [JsonPropertyName("uom")]
        public string Uom { get; set; } = "MT";

        [Column("open_qty")]
        [JsonPropertyName("open_qty")]
        public decimal OpenQty { get; set; }

        [Column("unit_price")]
        [JsonPropertyName("unit_price")]
        public decimal UnitPrice { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
