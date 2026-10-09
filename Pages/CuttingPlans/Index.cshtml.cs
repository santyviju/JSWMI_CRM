using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.CuttingPlans
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

        public List<CuttingPlan> CuttingPlans { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            var plans = await _supabaseService.GetCuttingPlansAsync();

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                plans = plans.Where(x => x.SapSyncStatus.Equals(StatusFilter, System.StringComparison.OrdinalIgnoreCase) ||
                                         x.Status.Equals(StatusFilter, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                plans = plans.Where(x =>
                    x.PlanCode.ToLowerInvariant().Contains(term) ||
                    x.SalesOrderDocNum.ToLowerInvariant().Contains(term) ||
                    x.CustomerName.ToLowerInvariant().Contains(term) ||
                    x.MotherCoilNo.ToLowerInvariant().Contains(term) ||
                    x.HeatNo.ToLowerInvariant().Contains(term)
                ).ToList();
            }

            CuttingPlans = plans;
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
                ErrorMessage = $"Failed to sync to SAP B1: {result.message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            await _supabaseService.DeleteCuttingPlanAsync(id);
            SuccessMessage = "Cutting Plan deleted successfully.";
            return RedirectToPage();
        }
    }
}
