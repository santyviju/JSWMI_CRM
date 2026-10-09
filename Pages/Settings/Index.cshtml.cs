using System.IO;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.Settings
{
    public class IndexModel : PageModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly ISapB1Service _sapB1Service;
        private readonly IWebHostEnvironment _env;

        public IndexModel(ISupabaseService supabaseService, ISapB1Service sapB1Service, IWebHostEnvironment env)
        {
            _supabaseService = supabaseService;
            _sapB1Service = sapB1Service;
            _env = env;
        }

        [BindProperty]
        public SupabaseConfig SupaConfig { get; set; } = new();

        [BindProperty]
        public SapB1Config SapConfig { get; set; } = new();

        public string SupabaseSqlSchema { get; set; } = string.Empty;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            SupaConfig = _supabaseService.GetConfig();
            SapConfig = _sapB1Service.GetConfig();

            LoadSqlSchema();
        }

        public IActionResult OnPostSaveSupabase()
        {
            _supabaseService.UpdateConfig(SupaConfig);
            SuccessMessage = "Supabase configuration updated.";
            return RedirectToPage();
        }

        public IActionResult OnPostSaveSap()
        {
            _sapB1Service.UpdateConfig(SapConfig);
            SuccessMessage = "SAP Business One Service Layer configuration updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTestSupabaseAsync()
        {
            _supabaseService.UpdateConfig(SupaConfig);
            var result = await _supabaseService.TestConnectionAsync();
            if (result.isConnected)
            {
                SuccessMessage = result.message;
            }
            else
            {
                ErrorMessage = result.message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTestSapAsync()
        {
            _sapB1Service.UpdateConfig(SapConfig);
            var result = await _sapB1Service.TestConnectionAsync();
            if (result.isSuccess)
            {
                SuccessMessage = result.message;
            }
            else
            {
                ErrorMessage = result.message;
            }

            return RedirectToPage();
        }

        private void LoadSqlSchema()
        {
            var schemaPath = Path.Combine(_env.ContentRootPath, "Database", "supabase_schema.sql");
            if (System.IO.File.Exists(schemaPath))
            {
                SupabaseSqlSchema = System.IO.File.ReadAllText(schemaPath);
            }
            else
            {
                SupabaseSqlSchema = "-- Schema file not found.";
            }
        }
    }
}
