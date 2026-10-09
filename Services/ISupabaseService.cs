using System.Collections.Generic;
using System.Threading.Tasks;
using JSWMI_CRM.Models;

namespace JSWMI_CRM.Services
{
    public interface ISupabaseService
    {
        Task<List<SalesOrder>> GetSalesOrdersAsync();
        Task<SalesOrder?> GetSalesOrderByIdAsync(string id);
        Task<SalesOrder> SaveSalesOrderAsync(SalesOrder order);
        Task<bool> DeleteSalesOrderAsync(string id);

        Task<List<CuttingPlan>> GetCuttingPlansAsync();
        Task<CuttingPlan?> GetCuttingPlanByIdAsync(string id);
        Task<CuttingPlan> SaveCuttingPlanAsync(CuttingPlan plan);
        Task<bool> DeleteCuttingPlanAsync(string id);
        Task UpdateCuttingPlanSapStatusAsync(string id, string status, string syncStatus, int? sapDocEntry, string? sapDocNum, string? message);

        Task<List<SapSyncLog>> GetSyncLogsAsync();
        Task AddSyncLogAsync(SapSyncLog log);

        SupabaseConfig GetConfig();
        void UpdateConfig(SupabaseConfig config);
        Task<(bool isConnected, string message)> TestConnectionAsync();
    }
}
