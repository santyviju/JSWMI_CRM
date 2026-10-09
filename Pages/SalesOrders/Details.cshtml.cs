using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.SalesOrders
{
    public class DetailsModel : PageModel
    {
        private readonly ISupabaseService _supabaseService;

        public DetailsModel(ISupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public SalesOrder? Order { get; set; }
        public List<CuttingPlan> LinkedCuttingPlans { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            Order = await _supabaseService.GetSalesOrderByIdAsync(id);
            if (Order == null)
            {
                return NotFound();
            }

            var allPlans = await _supabaseService.GetCuttingPlansAsync();
            LinkedCuttingPlans = allPlans.Where(x => x.SalesOrderId == id || x.SalesOrderDocNum == Order.DocNum).ToList();

            return Page();
        }
    }
}
