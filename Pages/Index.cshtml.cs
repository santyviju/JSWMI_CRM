using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly ISapB1Service _sapB1Service;

        public IndexModel(ISupabaseService supabaseService, ISapB1Service sapB1Service)
        {
            _supabaseService = supabaseService;
            _sapB1Service = sapB1Service;
        }

        public List<SalesOrder> RecentSalesOrders { get; set; } = new();
        public List<CuttingPlan> RecentCuttingPlans { get; set; } = new();
        public List<Location> Locations { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? LocationFilter { get; set; }

        public bool IsAdmin => User.IsInRole("Admin");
        public string? UserLocationName => User.FindFirst("LocationName")?.Value;

        public int TotalSalesOrders { get; set; }
        public int TotalCuttingPlans { get; set; }
        public int SyncedToSapCount { get; set; }
        public int PendingSyncCount { get; set; }
        public decimal TotalTonnagePlanned { get; set; }
        public decimal AverageYieldPercentage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Locations = await _supabaseService.GetLocationsAsync();

            var userLocationId = User.FindFirst("LocationId")?.Value;
            var effectiveLocationId = IsAdmin ? LocationFilter : userLocationId;

            var salesOrders = await _supabaseService.GetSalesOrdersAsync(effectiveLocationId, IsAdmin);
            var cuttingPlans = await _supabaseService.GetCuttingPlansAsync(effectiveLocationId, IsAdmin);

            RecentSalesOrders = salesOrders.Take(5).ToList();
            RecentCuttingPlans = cuttingPlans.Take(6).ToList();

            TotalSalesOrders = salesOrders.Count;
            TotalCuttingPlans = cuttingPlans.Count;
            SyncedToSapCount = cuttingPlans.Count(x => x.SapSyncStatus == "Synced");
            PendingSyncCount = cuttingPlans.Count(x => x.SapSyncStatus == "Pending" || x.Status == "Draft");
            TotalTonnagePlanned = cuttingPlans.Sum(x => x.TotalOutputWeight);

            if (cuttingPlans.Count > 0)
            {
                AverageYieldPercentage = Math.Round(cuttingPlans.Average(x => x.YieldPercentage), 1);
            }
            else
            {
                AverageYieldPercentage = 0;
            }
        }

        public async Task<IActionResult> OnPostSyncToSapAsync(string id)
        {
            var plan = await _supabaseService.GetCuttingPlanByIdAsync(id);
            if (plan == null)
            {
                ErrorMessage = "Cutting Plan not found.";
                return RedirectToPage();
            }

            var result = await _sapB1Service.PostCuttingPlanAsync(plan);
            if (result.isSuccess)
            {
                SuccessMessage = $"Successfully imported Cutting Plan {plan.PlanCode} to SAP B1. Production Order: {result.docNum} (DocEntry: {result.docEntry})";
            }
            else
            {
                ErrorMessage = $"Failed to import into SAP B1: {result.message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostFetchSapOrdersAsync()
        {
            var orders = await _sapB1Service.FetchSalesOrdersFromSapAsync();
            SuccessMessage = $"Refreshed {orders.Count} Sales Orders from SAP Business One.";
            return RedirectToPage();
        }
    }
}
