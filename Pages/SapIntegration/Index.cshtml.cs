using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.SapIntegration
{
    public class IndexModel : PageModel
    {
        private readonly ISapB1Service _sapB1Service;
        private readonly ISupabaseService _supabaseService;

        public IndexModel(ISapB1Service sapB1Service, ISupabaseService supabaseService)
        {
            _sapB1Service = sapB1Service;
            _supabaseService = supabaseService;
        }

        public SapB1Config Config { get; set; } = new();
        public List<SapSyncLog> Logs { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Config = _sapB1Service.GetConfig();
            Logs = await _supabaseService.GetSyncLogsAsync();
        }

        public async Task<IActionResult> OnPostTestConnectionAsync()
        {
            var testResult = await _sapB1Service.TestConnectionAsync();
            if (testResult.isSuccess)
            {
                SuccessMessage = testResult.message;
            }
            else
            {
                ErrorMessage = testResult.message;
            }

            return RedirectToPage();
        }

        public IActionResult OnPostToggleSimulation(bool enabled)
        {
            var cfg = _sapB1Service.GetConfig();
            cfg.SimulationMode = enabled;
            _sapB1Service.UpdateConfig(cfg);

            SuccessMessage = enabled
                ? "SAP B1 Sandbox / Simulation Mode ENABLED. API calls will simulate SAP B1 responses without network dependencies."
                : "SAP B1 Live API Mode ENABLED. API calls will target your configured SAP B1 Service Layer server.";

            return RedirectToPage();
        }
    }
}
