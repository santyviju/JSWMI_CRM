using System.Collections.Generic;
using System.Threading.Tasks;
using JSWMI_CRM.Models;

namespace JSWMI_CRM.Services
{
    public interface ISapB1Service
    {
        Task<(bool isSuccess, string message)> TestConnectionAsync();
        Task<(bool isSuccess, string message, int? docEntry, string? docNum)> PostCuttingPlanAsync(CuttingPlan plan);
        Task<List<SalesOrder>> FetchSalesOrdersFromSapAsync();
        SapB1Config GetConfig();
        void UpdateConfig(SapB1Config config);
    }
}
