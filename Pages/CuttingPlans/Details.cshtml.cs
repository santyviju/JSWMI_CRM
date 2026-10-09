using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.CuttingPlans
{
    public class DetailsModel : PageModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly ISapB1Service _sapB1Service;

        public DetailsModel(ISupabaseService supabaseService, ISapB1Service sapB1Service)
        {
            _supabaseService = supabaseService;
            _sapB1Service = sapB1Service;
        }

        public CuttingPlan? Plan { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            Plan = await _supabaseService.GetCuttingPlanByIdAsync(id);
            if (Plan == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSyncToSapAsync(string id)
        {
            var plan = await _supabaseService.GetCuttingPlanByIdAsync(id);
            if (plan == null)
            {
                return NotFound();
            }

            var result = await _sapB1Service.PostCuttingPlanAsync(plan);
            if (result.isSuccess)
            {
                SuccessMessage = $"Successfully imported to SAP Business One. Production Order: {result.docNum} (DocEntry: {result.docEntry})";
            }
            else
            {
                ErrorMessage = $"Failed to sync to SAP B1: {result.message}";
            }

            return RedirectToPage(new { id });
        }
    }
}
