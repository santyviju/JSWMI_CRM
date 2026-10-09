using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using Microsoft.AspNetCore.Http;

namespace JSWMI_CRM.Services
{
    public interface ICuttingPlanImportService
    {
        Task<List<CuttingPlanLine>> ParseImportFileAsync(IFormFile file);
        byte[] GenerateSampleExcelTemplate();
        string GenerateSampleCsvTemplate();
    }
}
