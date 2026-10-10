using System.Collections.Generic;
using System.Threading.Tasks;
using JSWMI_CRM.Models;

namespace JSWMI_CRM.Services
{
    public interface ISupabaseService
    {
        Task<List<SalesOrder>> GetSalesOrdersAsync(string? locationId = null, bool isAdmin = true);
        Task<SalesOrder?> GetSalesOrderByIdAsync(string id);
        Task<SalesOrder> SaveSalesOrderAsync(SalesOrder order);
        Task<bool> DeleteSalesOrderAsync(string id);

        Task<List<CuttingPlan>> GetCuttingPlansAsync(string? locationId = null, bool isAdmin = true, string? userId = null);
        Task<CuttingPlan?> GetCuttingPlanByIdAsync(string id);
        Task<CuttingPlan> SaveCuttingPlanAsync(CuttingPlan plan);
        Task<bool> DeleteCuttingPlanAsync(string id);
        Task UpdateCuttingPlanSapStatusAsync(string id, string status, string syncStatus, int? sapDocEntry, string? sapDocNum, string? message);

        Task<List<Location>> GetLocationsAsync(bool onlyActive = false);
        Task<Location?> GetLocationByIdAsync(string id);
        Task<Location> SaveLocationAsync(Location location);
        Task<bool> DeleteLocationAsync(string id);

        Task<UserProfile?> GetUserProfileAsync(string userId);
        Task<UserProfile> SaveUserProfileAsync(UserProfile profile);
        Task<List<UserProfile>> GetAllUserProfilesAsync();

        Task EnsureDatabaseSchemaAsync();

        Task<List<SapSyncLog>> GetSyncLogsAsync();
        Task AddSyncLogAsync(SapSyncLog log);

        SupabaseConfig GetConfig();
        void UpdateConfig(SupabaseConfig config);
        Task<(bool isConnected, string message)> TestConnectionAsync();
        Task<(bool success, string? userId, string? email, string? error)> AuthenticateUserAsync(string email, string password);
    }
}
