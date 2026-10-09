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

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public SalesOrder NewOrder { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            var all = await _supabaseService.GetSalesOrdersAsync();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                SalesOrders = all.Where(x =>
                    x.DocNum.ToLowerInvariant().Contains(term) ||
                    x.CardName.ToLowerInvariant().Contains(term) ||
                    x.CardCode.ToLowerInvariant().Contains(term)
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
            NewOrder.CreatedAt = DateTime.UtcNow;
            NewOrder.UpdatedAt = DateTime.UtcNow;

            await _supabaseService.SaveSalesOrderAsync(NewOrder);
            SuccessMessage = $"Sales Order {NewOrder.DocNum} created successfully.";
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
