using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.SalesOrders
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

        public List<SalesOrder> SalesOrders { get; set; } = new();
        public List<Location> Locations { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? LocationFilter { get; set; }

        public bool IsAdmin => User.IsInRole("Admin");
        public string? UserLocationName => User.FindFirst("LocationName")?.Value;

        [BindProperty]
        public SalesOrder NewOrder { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Locations = await _supabaseService.GetLocationsAsync();

            var userLocationId = User.FindFirst("LocationId")?.Value;
            var effectiveLocationId = IsAdmin ? LocationFilter : userLocationId;

            var all = await _supabaseService.GetSalesOrdersAsync(effectiveLocationId, IsAdmin);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                SalesOrders = all.Where(x =>
                    x.DocNum.ToLowerInvariant().Contains(term) ||
                    x.CardName.ToLowerInvariant().Contains(term) ||
                    x.CardCode.ToLowerInvariant().Contains(term) ||
                    (x.CreatedByName != null && x.CreatedByName.ToLowerInvariant().Contains(term)) ||
                    (x.LocationName != null && x.LocationName.ToLowerInvariant().Contains(term))
                ).ToList();
            }
            else
            {
                SalesOrders = all;
            }
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (string.IsNullOrWhiteSpace(NewOrder.CardName) || string.IsNullOrWhiteSpace(NewOrder.DocNum))
            {
                ErrorMessage = "Customer Name and Order Number are required.";
                return RedirectToPage();
            }

            NewOrder.Id = "SO-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            NewOrder.CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            NewOrder.CreatedByName = User.Identity?.Name ?? "Sales Planner";

            if (string.IsNullOrEmpty(NewOrder.LocationId))
            {
                NewOrder.LocationId = User.FindFirst("LocationId")?.Value;
                NewOrder.LocationName = User.FindFirst("LocationName")?.Value;
            }
            else
            {
                var loc = await _supabaseService.GetLocationByIdAsync(NewOrder.LocationId);
                NewOrder.LocationName = loc?.Name;
            }

            NewOrder.CreatedAt = DateTime.UtcNow;
            NewOrder.UpdatedAt = DateTime.UtcNow;

            await _supabaseService.SaveSalesOrderAsync(NewOrder);
            SuccessMessage = $"Sales Order {NewOrder.DocNum} created successfully for {NewOrder.LocationName ?? "JSW Service Center"}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSyncFromSapAsync()
        {
            try
            {
                var orders = await _sapB1Service.FetchSalesOrdersFromSapAsync();
                SuccessMessage = $"Successfully synced {orders.Count} open Sales Orders from SAP Business One.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to fetch orders from SAP B1: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
