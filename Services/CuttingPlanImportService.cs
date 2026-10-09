using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using JSWMI_CRM.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JSWMI_CRM.Services
{
    public class CuttingPlanImportService : ICuttingPlanImportService
    {
        private readonly ILogger<CuttingPlanImportService> _logger;

        public CuttingPlanImportService(ILogger<CuttingPlanImportService> logger)
        {
            _logger = logger;
        }

        public async Task<List<CuttingPlanLine>> ParseImportFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("Uploaded file is empty or missing.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var results = new List<CuttingPlanLine>();

            if (extension == ".xlsx" || extension == ".xls")
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;
                results = ParseExcel(stream);
            }
            else if (extension == ".csv")
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;
                results = ParseCsv(stream);
            }
            else
            {
                throw new NotSupportedException("Only Excel (.xlsx) or CSV (.csv) files are supported for Cutting Plan import.");
            }

            return results;
        }

        private List<CuttingPlanLine> ParseExcel(Stream stream)
        {
            var lines = new List<CuttingPlanLine>();

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1); // First worksheet
            var rows = worksheet.RangeUsed()?.RowsUsed();

            if (rows == null)
            {
                return lines;
            }

            bool headerFound = false;
            int colLineNo = 1, colItemCode = 2, colDesc = 3, colWidth = 4, colLength = 5, colQty = 6, colWeight = 7, colTolerance = 8, colEdge = 9, colRemarks = 10;

            int currentLine = 1;
            foreach (var row in rows)
            {
                if (!headerFound)
                {
                    // Scan header columns
                    for (int c = 1; c <= row.LastCellUsed()?.Address.ColumnNumber; c++)
                    {
                        var cellVal = row.Cell(c).GetString().Trim().ToLowerInvariant();
                        if (cellVal.Contains("line") || cellVal.Contains("slit") || cellVal.Contains("sr")) colLineNo = c;
                        else if (cellVal.Contains("item") || cellVal.Contains("code")) colItemCode = c;
                        else if (cellVal.Contains("desc") || cellVal.Contains("spec")) colDesc = c;
                        else if (cellVal.Contains("width")) colWidth = c;
                        else if (cellVal.Contains("length")) colLength = c;
                        else if (cellVal.Contains("qty") || cellVal.Contains("pieces") || cellVal.Contains("cuts")) colQty = c;
                        else if (cellVal.Contains("weight")) colWeight = c;
                        else if (cellVal.Contains("toler")) colTolerance = c;
                        else if (cellVal.Contains("edge")) colEdge = c;
                        else if (cellVal.Contains("remark") || cellVal.Contains("note")) colRemarks = c;
                    }
                    headerFound = true;
                    continue;
                }

                // Data row
                var itemCode = row.Cell(colItemCode).GetString().Trim();
                var widthVal = row.Cell(colWidth).GetString().Trim();

                if (string.IsNullOrWhiteSpace(itemCode) && string.IsNullOrWhiteSpace(widthVal))
                {
                    continue; // Skip empty rows
                }

                decimal.TryParse(widthVal, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal width);
                decimal.TryParse(row.Cell(colLength).GetString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal length);
                int.TryParse(row.Cell(colQty).GetString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int qty);
                if (qty <= 0) qty = 1;

                decimal.TryParse(row.Cell(colWeight).GetString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal weight);

                var description = row.Cell(colDesc).GetString().Trim();
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = $"Slit {width}mm Steel Coil";
                }

                var tolerance = row.Cell(colTolerance).GetString().Trim();
                if (string.IsNullOrWhiteSpace(tolerance)) tolerance = "+/- 0.5 mm";

                var edge = row.Cell(colEdge).GetString().Trim();
                if (string.IsNullOrWhiteSpace(edge)) edge = "Slit Edge";

                var remarks = row.Cell(colRemarks).GetString().Trim();

                lines.Add(new CuttingPlanLine
                {
                    LineNum = currentLine++,
                    OutputItemCode = string.IsNullOrWhiteSpace(itemCode) ? $"SLIT-{width:0}" : itemCode,
                    OutputDescription = description,
                    CutWidth = width,
                    CutLength = length,
                    Quantity = qty,
                    TargetWeight = weight,
                    Tolerance = tolerance,
                    EdgeCondition = edge,
                    Remarks = remarks
                });
            }

            return lines;
        }

        private List<CuttingPlanLine> ParseCsv(Stream stream)
        {
            var lines = new List<CuttingPlanLine>();

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null,
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true
            };

            using var csv = new CsvReader(reader, config);
            var records = csv.GetRecords<CuttingPlanImportDto>();

            int currentLine = 1;
            foreach (var rec in records)
            {
                if (rec.Width <= 0 && string.IsNullOrWhiteSpace(rec.OutputItemCode))
                {
                    continue;
                }

                lines.Add(new CuttingPlanLine
                {
                    LineNum = rec.LineNo > 0 ? rec.LineNo : currentLine++,
                    OutputItemCode = string.IsNullOrWhiteSpace(rec.OutputItemCode) ? $"SLIT-{rec.Width:0}" : rec.OutputItemCode,
                    OutputDescription = string.IsNullOrWhiteSpace(rec.Description) ? $"Slit {rec.Width}mm Steel Coil" : rec.Description,
                    CutWidth = rec.Width,
                    CutLength = rec.Length,
                    Quantity = rec.Quantity > 0 ? rec.Quantity : 1,
                    TargetWeight = rec.TargetWeight,
                    Tolerance = string.IsNullOrWhiteSpace(rec.Tolerance) ? "+/- 0.5 mm" : rec.Tolerance,
                    EdgeCondition = string.IsNullOrWhiteSpace(rec.EdgeCondition) ? "Slit Edge" : rec.EdgeCondition,
                    Remarks = rec.Remarks
                });
            }

            return lines;
        }

        public byte[] GenerateSampleExcelTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Cutting Plan Template");

            // Headers
            string[] headers = {
                "LineNo", "OutputItemCode", "OutputDescription", "CutWidth(mm)",
                "CutLength(mm)", "Quantity", "TargetWeight(MT)", "Tolerance",
                "EdgeCondition", "Remarks"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 51, 102); // JSW Dark Blue
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Sample rows
            object[,] sampleData = {
                { 1, "JSW-SLT-320", "Automotive Door Inner Slit Strip", 320.0, 0, 1, 6.450, "+/- 0.3 mm", "Slit Edge", "High priority batch A" },
                { 2, "JSW-SLT-450", "Automotive Roof Bow Slit Strip", 450.0, 0, 1, 9.070, "+/- 0.5 mm", "Slit Edge", "Customer spec D3" },
                { 3, "JSW-SLT-280", "Reinforcement Rail Outer Strip", 280.0, 0, 1, 5.640, "+/- 0.5 mm", "Slit Edge", "Mill edge trim removed" },
                { 4, "JSW-SLT-180", "Bracket Bracket Support Blanks", 180.0, 0, 1, 3.630, "+/- 0.5 mm", "Slit Edge", "Balanced strip" }
            };

            for (int r = 0; r < sampleData.GetLength(0); r++)
            {
                for (int c = 0; c < sampleData.GetLength(1); c++)
                {
                    worksheet.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(sampleData[r, c]);
                }
            }

            worksheet.Columns().AdjustToContents();

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }

        public string GenerateSampleCsvTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("LineNo,OutputItemCode,Description,Width,Length,Quantity,TargetWeight,Tolerance,EdgeCondition,Remarks");
            sb.AppendLine("1,JSW-SLT-320,Automotive Door Inner Slit Strip,320,0,1,6.45,+/- 0.3 mm,Slit Edge,High priority batch A");
            sb.AppendLine("2,JSW-SLT-450,Automotive Roof Bow Slit Strip,450,0,1,9.07,+/- 0.5 mm,Slit Edge,Customer spec D3");
            sb.AppendLine("3,JSW-SLT-280,Reinforcement Rail Outer Strip,280,0,1,5.64,+/- 0.5 mm,Slit Edge,Mill edge trim removed");
            sb.AppendLine("4,JSW-SLT-180,Bracket Support Blanks,180,0,1,3.63,+/- 0.5 mm,Slit Edge,Balanced strip");
            return sb.ToString();
        }
    }
}
