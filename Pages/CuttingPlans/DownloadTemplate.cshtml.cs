using System.Text;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.CuttingPlans
{
    public class DownloadTemplateModel : PageModel
    {
        private readonly ICuttingPlanImportService _importService;

        public DownloadTemplateModel(ICuttingPlanImportService importService)
        {
            _importService = importService;
        }

        public void OnGet()
        {
        }

        public IActionResult OnGetExcel()
        {
            var bytes = _importService.GenerateSampleExcelTemplate();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "JSWMI_Cutting_Plan_Template.xlsx");
        }

        public IActionResult OnGetCsv()
        {
            var csv = _importService.GenerateSampleCsvTemplate();
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "JSWMI_Cutting_Plan_Template.csv");
        }
    }
}
