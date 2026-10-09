using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JSWMI_CRM.Models
{
    [Table("cutting_plans")]
    public class CuttingPlan
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        [Column("plan_code")]
        [JsonPropertyName("plan_code")]
        public string PlanCode { get; set; } = string.Empty;

        [Column("sales_order_id")]
        [JsonPropertyName("sales_order_id")]
        public string? SalesOrderId { get; set; }

        [Required]
        [Column("sales_order_doc_num")]
        [JsonPropertyName("sales_order_doc_num")]
        public string SalesOrderDocNum { get; set; } = string.Empty;

        [Required]
        [Column("customer_name")]
        [JsonPropertyName("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [Column("mother_coil_no")]
        [JsonPropertyName("mother_coil_no")]
        public string MotherCoilNo { get; set; } = string.Empty;

        [Required]
        [Column("heat_no")]
        [JsonPropertyName("heat_no")]
        public string HeatNo { get; set; } = string.Empty;

        [Required]
        [Column("grade")]
        [JsonPropertyName("grade")]
        public string Grade { get; set; } = string.Empty;

        [Column("mother_width")]
        [JsonPropertyName("mother_width")]
        public decimal MotherWidth { get; set; } // in mm

        [Column("mother_thickness")]
        [JsonPropertyName("mother_thickness")]
        public decimal MotherThickness { get; set; } // in mm

        [Column("mother_weight")]
        [JsonPropertyName("mother_weight")]
        public decimal MotherWeight { get; set; } // in MT

        [Column("total_output_width")]
        [JsonPropertyName("total_output_width")]
        public decimal TotalOutputWidth { get; set; }

        [Column("scrap_width")]
        [JsonPropertyName("scrap_width")]
        public decimal ScrapWidth { get; set; }

        [Column("total_output_weight")]
        [JsonPropertyName("total_output_weight")]
        public decimal TotalOutputWeight { get; set; }

        [Column("yield_percentage")]
        [JsonPropertyName("yield_percentage")]
        public decimal YieldPercentage { get; set; }

        [Column("status")]
        [JsonPropertyName("status")]
        public string Status { get; set; } = "Draft"; // Draft, Validated, Sent to SAP B1, Sync Error

        [Column("sap_doc_entry")]
        [JsonPropertyName("sap_doc_entry")]
        public int? SapDocEntry { get; set; }

        [Column("sap_doc_num")]
        [JsonPropertyName("sap_doc_num")]
        public string? SapDocNum { get; set; }

        [Column("sap_sync_status")]
        [JsonPropertyName("sap_sync_status")]
        public string SapSyncStatus { get; set; } = "Pending"; // Pending, Synced, Failed

        [Column("sap_sync_message")]
        [JsonPropertyName("sap_sync_message")]
        public string? SapSyncMessage { get; set; }

        [Column("sap_sync_timestamp")]
        [JsonPropertyName("sap_sync_timestamp")]
        public DateTime? SapSyncTimestamp { get; set; }

        [Column("remarks")]
        [JsonPropertyName("remarks")]
        public string? Remarks { get; set; }

        [Column("created_by")]
        [JsonPropertyName("created_by")]
        public string CreatedBy { get; set; } = "Planner";

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lines")]
        public List<CuttingPlanLine> Lines { get; set; } = new();

        public void RecalculateTotals()
        {
            decimal sumWidth = 0;
            decimal sumWeight = 0;

            foreach (var line in Lines)
            {
                sumWidth += line.CutWidth * Math.Max(line.Quantity, 1);
                sumWeight += line.TargetWeight;
            }

            TotalOutputWidth = sumWidth;
            ScrapWidth = Math.Max(0, MotherWidth - sumWidth);
            TotalOutputWeight = sumWeight;

            if (MotherWeight > 0)
            {
                YieldPercentage = Math.Round((sumWeight / MotherWeight) * 100, 2);
            }
            else if (MotherWidth > 0)
            {
                YieldPercentage = Math.Round((sumWidth / MotherWidth) * 100, 2);
            }
            else
            {
                YieldPercentage = 0;
            }
        }
    }

    [Table("cutting_plan_lines")]
    public class CuttingPlanLine
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Column("cutting_plan_id")]
        [JsonPropertyName("cutting_plan_id")]
        public string CuttingPlanId { get; set; } = string.Empty;

        [Column("line_num")]
        [JsonPropertyName("line_num")]
        public int LineNum { get; set; }

        [Required]
        [Column("output_item_code")]
        [JsonPropertyName("output_item_code")]
        public string OutputItemCode { get; set; } = string.Empty;

        [Column("output_description")]
        [JsonPropertyName("output_description")]
        public string OutputDescription { get; set; } = string.Empty;

        [Column("cut_width")]
        [JsonPropertyName("cut_width")]
        public decimal CutWidth { get; set; } // in mm

        [Column("cut_length")]
        [JsonPropertyName("cut_length")]
        public decimal CutLength { get; set; } // in mm (0 for coil slit)

        [Column("quantity")]
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [Column("target_weight")]
        [JsonPropertyName("target_weight")]
        public decimal TargetWeight { get; set; } // in MT

        [Column("tolerance")]
        [JsonPropertyName("tolerance")]
        public string Tolerance { get; set; } = "+/- 0.5 mm";

        [Column("edge_condition")]
        [JsonPropertyName("edge_condition")]
        public string EdgeCondition { get; set; } = "Slit Edge"; // Slit Edge, Mill Edge

        [Column("remarks")]
        [JsonPropertyName("remarks")]
        public string? Remarks { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
