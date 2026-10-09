using CsvHelper.Configuration.Attributes;

namespace JSWMI_CRM.Models
{
    public class CuttingPlanImportDto
    {
        [Name("LineNo", "Line", "SlitNo", "SrNo")]
        public int LineNo { get; set; }

        [Name("OutputItemCode", "ItemCode", "Item No")]
        public string OutputItemCode { get; set; } = string.Empty;

        [Name("Description", "OutputDescription", "ItemDescription", "Spec")]
        public string Description { get; set; } = string.Empty;

        [Name("Width", "CutWidth", "Width(mm)", "Width mm")]
        public decimal Width { get; set; }

        [Name("Length", "CutLength", "Length(mm)", "Length mm")]
        public decimal Length { get; set; } = 0;

        [Name("Quantity", "Qty", "NoOfSlits", "Pcs")]
        public int Quantity { get; set; } = 1;

        [Name("TargetWeight", "Weight", "Weight(MT)", "TargetWeight(MT)")]
        public decimal TargetWeight { get; set; }

        [Name("Tolerance", "WidthTolerance")]
        public string Tolerance { get; set; } = "+/- 0.5 mm";

        [Name("EdgeCondition", "Edge", "Edge Condition")]
        public string EdgeCondition { get; set; } = "Slit Edge";

        [Name("Remarks", "Notes", "Tag")]
        public string? Remarks { get; set; }
    }

    public class CuttingPlanHeaderImportDto
    {
        public string MotherCoilNo { get; set; } = string.Empty;
        public string HeatNo { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public decimal MotherWidth { get; set; }
        public decimal MotherThickness { get; set; }
        public decimal MotherWeight { get; set; }
        public string? Remarks { get; set; }
    }
}
