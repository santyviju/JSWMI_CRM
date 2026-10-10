using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace JSWMI_CRM.Pages.CuttingPlans
{
    public class CreateModel : PageModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly ISapB1Service _sapB1Service;
        private readonly ICuttingPlanImportService _importService;

        public CreateModel(
            ISupabaseService supabaseService,
            ISapB1Service sapB1Service,
            ICuttingPlanImportService importService)
        {
            _supabaseService = supabaseService;
            _sapB1Service = sapB1Service;
            _importService = importService;
        }

        [BindProperty(SupportsGet = true)]
        public string? SalesOrderId { get; set; }

        public List<SelectListItem> SalesOrderOptions { get; set; } = new();
        public List<SelectListItem> LocationOptions { get; set; } = new();

        public List<SalesOrder> AvailableSalesOrders { get; set; } = new();

        public bool IsAdmin => User.IsInRole("Admin");

        [BindProperty]
        public CuttingPlan Plan { get; set; } = new();

        [BindProperty]
        public string LinesJson { get; set; } = "[]";

        [BindProperty]
        public IFormFile? UploadFile { get; set; }

        [BindProperty]
        public bool PushToSapImmediately { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadDropdownsAsync();

            Plan.PreparedByName = User.Identity?.Name ?? "Planner";
            Plan.PreparedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Plan.LocationId = User.FindFirst("LocationId")?.Value;
            Plan.LocationName = User.FindFirst("LocationName")?.Value;

            if (string.IsNullOrEmpty(Plan.LocationId) && LocationOptions.Count > 0)
            {
                Plan.LocationId = LocationOptions[0].Value;
                Plan.LocationName = LocationOptions[0].Text;
            }

            if (!string.IsNullOrEmpty(SalesOrderId))
            {
                var so = await _supabaseService.GetSalesOrderByIdAsync(SalesOrderId);
                if (so != null)
                {
                    Plan.SalesOrderId = so.Id;
                    Plan.SalesOrderDocNum = so.DocNum;
                    Plan.CustomerName = so.CardName;
                    if (!string.IsNullOrEmpty(so.LocationId))
                    {
                        Plan.LocationId = so.LocationId;
                        Plan.LocationName = so.LocationName;
                    }

                    if (so.Items.Count > 0)
                    {
                        var firstItem = so.Items[0];
                        Plan.Grade = "CR Grade D";
                        Plan.MotherWidth = 1250;
                        Plan.MotherThickness = 1.20m;
                        Plan.MotherWeight = Math.Min(firstItem.OpenQty > 0 ? firstItem.OpenQty : 25, 28);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(Plan.PlanCode))
            {
                Plan.PlanCode = $"CP-JSW-{DateTime.UtcNow:yyMMdd}-{new Random().Next(100, 999)}";
            }
            if (string.IsNullOrWhiteSpace(Plan.MotherCoilNo))
            {
                Plan.MotherCoilNo = $"MC-JSW-2026-{new Random().Next(1000, 9999)}";
            }
            if (string.IsNullOrWhiteSpace(Plan.HeatNo))
            {
                Plan.HeatNo = $"HT-{new Random().Next(400000, 999999)}";
            }
            if (Plan.MotherWidth == 0) Plan.MotherWidth = 1250;
            if (Plan.MotherThickness == 0) Plan.MotherThickness = 1.20m;
            if (Plan.MotherWeight == 0) Plan.MotherWeight = 25.000m;
            if (string.IsNullOrWhiteSpace(Plan.Grade)) Plan.Grade = "CR EDD Grade D";

            // Initialize with sample lines if empty
            if (string.IsNullOrWhiteSpace(LinesJson) || LinesJson == "[]")
            {
                var defaultLines = new List<CuttingPlanLine>
                {
                    new() { LineNum = 1, OutputItemCode = "JSW-SLT-400", OutputDescription = "Automotive Body Slit 400mm", CutWidth = 400, CutLength = 0, Quantity = 1, TargetWeight = 8.000m, Tolerance = "+/- 0.3 mm", EdgeCondition = "Slit Edge", Remarks = "Stamping A" },
                    new() { LineNum = 2, OutputItemCode = "JSW-SLT-420", OutputDescription = "Automotive Body Slit 420mm", CutWidth = 420, CutLength = 0, Quantity = 1, TargetWeight = 8.400m, Tolerance = "+/- 0.3 mm", EdgeCondition = "Slit Edge", Remarks = "Stamping B" },
                    new() { LineNum = 3, OutputItemCode = "JSW-SLT-410", OutputDescription = "Reinforcement Slit 410mm", CutWidth = 410, CutLength = 0, Quantity = 1, TargetWeight = 8.200m, Tolerance = "+/- 0.3 mm", EdgeCondition = "Slit Edge", Remarks = "Reinforcement" }
                };
                LinesJson = JsonSerializer.Serialize(defaultLines);
            }
        }

        public async Task<IActionResult> OnPostUploadFileAsync()
        {
            await LoadDropdownsAsync();

            if (UploadFile == null || UploadFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a valid Excel (.xlsx) or CSV (.csv) file to import.");
                return Page();
            }

            try
            {
                var parsedLines = await _importService.ParseImportFileAsync(UploadFile);
                if (parsedLines.Count == 0)
                {
                    ErrorMessage = "No cutting plan rows were found in the uploaded file.";
                }
                else
                {
                    LinesJson = JsonSerializer.Serialize(parsedLines);
                    SuccessMessage = $"Successfully imported {parsedLines.Count} slit rows from {UploadFile.FileName}.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error parsing file: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            await LoadDropdownsAsync();

            // Resolve Sales Order information
            if (!string.IsNullOrEmpty(Plan.SalesOrderId))
            {
                var so = await _supabaseService.GetSalesOrderByIdAsync(Plan.SalesOrderId);
                if (so != null)
                {
                    Plan.SalesOrderDocNum = so.DocNum;
                    Plan.CustomerName = so.CardName;
                }
            }

            if (string.IsNullOrWhiteSpace(Plan.SalesOrderDocNum))
            {
                ModelState.AddModelError("Plan.SalesOrderId", "Please select a Sales Order for this cutting plan.");
                return Page();
            }

            // Deserialize lines
            List<CuttingPlanLine> lines;
            try
            {
                lines = JsonSerializer.Deserialize<List<CuttingPlanLine>>(LinesJson) ?? new List<CuttingPlanLine>();
            }
            catch
            {
                lines = new List<CuttingPlanLine>();
            }

            if (lines.Count == 0)
            {
                ModelState.AddModelError("", "Please provide at least one slit / cut line item in the cutting plan.");
                return Page();
            }

            // Assign lines
            Plan.Lines = lines;
            Plan.RecalculateTotals();

            // Validate width constraint
            if (Plan.TotalOutputWidth > Plan.MotherWidth)
            {
                ModelState.AddModelError("", $"Total slit width ({Plan.TotalOutputWidth} mm) exceeds Mother Coil width ({Plan.MotherWidth} mm) by {Plan.TotalOutputWidth - Plan.MotherWidth} mm!");
                return Page();
            }

            // Set audit tracking: Prepared By & Location
            if (string.IsNullOrEmpty(Plan.PreparedByUserId))
            {
                Plan.PreparedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }
            if (string.IsNullOrEmpty(Plan.PreparedByName))
            {
                Plan.PreparedByName = User.Identity?.Name ?? "Planner";
            }

            if (!string.IsNullOrEmpty(Plan.LocationId))
            {
                var loc = await _supabaseService.GetLocationByIdAsync(Plan.LocationId);
                Plan.LocationName = loc?.Name;
            }
            else
            {
                Plan.LocationId = User.FindFirst("LocationId")?.Value;
                Plan.LocationName = User.FindFirst("LocationName")?.Value;
            }

            // Save to Supabase (and local store)
            var savedPlan = await _supabaseService.SaveCuttingPlanAsync(Plan);

            if (PushToSapImmediately)
            {
                var syncResult = await _sapB1Service.PostCuttingPlanAsync(savedPlan);
                if (syncResult.isSuccess)
                {
                    SuccessMessage = $"Cutting Plan {savedPlan.PlanCode} saved and successfully imported to SAP B1 as Production Order {syncResult.docNum} (DocEntry: {syncResult.docEntry})!";
                }
                else
                {
                    ErrorMessage = $"Cutting Plan saved, but SAP B1 sync returned error: {syncResult.message}";
                }
            }
            else
            {
                SuccessMessage = $"Cutting Plan {savedPlan.PlanCode} saved as Draft in Supabase.";
            }

            return RedirectToPage("/CuttingPlans/Details", new { id = savedPlan.Id });
        }

        private async Task LoadDropdownsAsync()
        {
            var userLocationId = User.FindFirst("LocationId")?.Value;
            bool isAdmin = User.IsInRole("Admin");

            AvailableSalesOrders = await _supabaseService.GetSalesOrdersAsync(userLocationId, isAdmin);
            SalesOrderOptions = AvailableSalesOrders.Select(x => new SelectListItem
            {
                Value = x.Id,
                Text = $"{x.DocNum} - {x.CardName} (Due: {x.DocDueDate:dd-MMM}) {(string.IsNullOrEmpty(x.LocationName) ? "" : $"[{x.LocationName}]")}"
            }).ToList();

            var locations = await _supabaseService.GetLocationsAsync();
            LocationOptions = locations.Where(l => l.IsActive).Select(l => new SelectListItem
            {
                Value = l.Id,
                Text = $"{l.Code} - {l.Name}"
            }).ToList();
        }
    }
}
