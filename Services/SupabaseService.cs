using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace JSWMI_CRM.Services
{
    public class SupabaseService : ISupabaseService
    {
        private readonly ILogger<SupabaseService> _logger;
        private readonly IConfiguration _configuration;
        private SupabaseConfig _config;

        // In-memory persistent caches for ultra-fast UI rendering & offline development
        private static readonly ConcurrentDictionary<string, SalesOrder> _salesOrders = new();
        private static readonly ConcurrentDictionary<string, CuttingPlan> _cuttingPlans = new();
        private static readonly List<SapSyncLog> _syncLogs = new();
        private static readonly object _syncLock = new();

        public SupabaseService(ILogger<SupabaseService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _config = new SupabaseConfig
            {
                Url = _configuration["Supabase:Url"] ?? "https://your-project.supabase.co",
                Key = _configuration["Supabase:Key"] ?? "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                ConnectionString = _configuration["Supabase:ConnectionString"],
                UseCloudStorage = bool.TryParse(_configuration["Supabase:UseCloudStorage"], out var ucs) && ucs
            };

            EnsureSeedData();
        }

        public SupabaseConfig GetConfig() => _config;

        public void UpdateConfig(SupabaseConfig config)
        {
            _config = config;
        }

        public async Task<(bool isConnected, string message)> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_config.Url) || _config.Url.Contains("your-project"))
            {
                return (false, "Supabase URL is not configured. Please enter your valid Supabase project URL.");
            }

            if (string.IsNullOrWhiteSpace(_config.Key) || _config.Key.Contains("..."))
            {
                return (false, "Supabase API Key is not configured. Please provide your Supabase anon or service key.");
            }

            try
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(_config.Url);
                client.DefaultRequestHeaders.Add("apikey", _config.Key);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.Key}");

                var response = await client.GetAsync("rest/v1/");
                if (response.IsSuccessStatusCode)
                {
                    return (true, $"Successfully connected to Supabase Cloud REST endpoint ({response.StatusCode}).");
                }
                else
                {
                    return (false, $"Supabase responded with status code: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Supabase Cloud");
                return (false, $"Connection error: {ex.Message}");
            }
        }

        public Task<List<SalesOrder>> GetSalesOrdersAsync()
        {
            var list = _salesOrders.Values.OrderByDescending(x => x.DocDate).ToList();
            return Task.FromResult(list);
        }

        public Task<SalesOrder?> GetSalesOrderByIdAsync(string id)
        {
            _salesOrders.TryGetValue(id, out var order);
            return Task.FromResult(order);
        }

        public Task<SalesOrder> SaveSalesOrderAsync(SalesOrder order)
        {
            if (string.IsNullOrWhiteSpace(order.Id))
            {
                order.Id = "SO-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            }

            order.UpdatedAt = DateTime.UtcNow;
            _salesOrders[order.Id] = order;
            return Task.FromResult(order);
        }

        public Task<bool> DeleteSalesOrderAsync(string id)
        {
            return Task.FromResult(_salesOrders.TryRemove(id, out _));
        }

        public Task<List<CuttingPlan>> GetCuttingPlansAsync()
        {
            var list = _cuttingPlans.Values.OrderByDescending(x => x.CreatedAt).ToList();
            return Task.FromResult(list);
        }

        public Task<CuttingPlan?> GetCuttingPlanByIdAsync(string id)
        {
            _cuttingPlans.TryGetValue(id, out var plan);
            return Task.FromResult(plan);
        }

        public Task<CuttingPlan> SaveCuttingPlanAsync(CuttingPlan plan)
        {
            if (string.IsNullOrWhiteSpace(plan.Id))
            {
                plan.Id = "CP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            }

            if (string.IsNullOrWhiteSpace(plan.PlanCode))
            {
                plan.PlanCode = $"CP-JSW-{DateTime.UtcNow:yyMMdd}-{new Random().Next(100, 999)}";
            }

            plan.RecalculateTotals();
            plan.UpdatedAt = DateTime.UtcNow;
            _cuttingPlans[plan.Id] = plan;
            return Task.FromResult(plan);
        }

        public Task<bool> DeleteCuttingPlanAsync(string id)
        {
            return Task.FromResult(_cuttingPlans.TryRemove(id, out _));
        }

        public Task UpdateCuttingPlanSapStatusAsync(string id, string status, string syncStatus, int? sapDocEntry, string? sapDocNum, string? message)
        {
            if (_cuttingPlans.TryGetValue(id, out var plan))
            {
                plan.Status = status;
                plan.SapSyncStatus = syncStatus;
                plan.SapDocEntry = sapDocEntry;
                plan.SapDocNum = sapDocNum;
                plan.SapSyncMessage = message;
                plan.SapSyncTimestamp = DateTime.UtcNow;
                plan.UpdatedAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task<List<SapSyncLog>> GetSyncLogsAsync()
        {
            lock (_syncLock)
            {
                var list = _syncLogs.OrderByDescending(x => x.CreatedAt).Take(100).ToList();
                return Task.FromResult(list);
            }
        }

        public Task AddSyncLogAsync(SapSyncLog log)
        {
            lock (_syncLock)
            {
                _syncLogs.Insert(0, log);
                if (_syncLogs.Count > 200)
                {
                    _syncLogs.RemoveAt(_syncLogs.Count - 1);
                }
            }
            return Task.CompletedTask;
        }

        private void EnsureSeedData()
        {
            if (_salesOrders.IsEmpty)
            {
                var so1 = new SalesOrder
                {
                    Id = "SO-1001",
                    DocNum = "SO-2026-0891",
                    DocEntry = 891,
                    CardCode = "C00102",
                    CardName = "Tata Motors Passenger Vehicles Ltd",
                    DocDate = DateTime.Today.AddDays(-2),
                    DocDueDate = DateTime.Today.AddDays(10),
                    DocTotal = 1850000.00m,
                    Currency = "INR",
                    Status = "Open",
                    Remarks = "High tensile CR steel required for automotive body stamping",
                    Items = new List<SalesOrderItem>
                    {
                        new() { Id = "SOI-101", SalesOrderId = "SO-1001", LineNum = 1, ItemCode = "JSW-CR-1250-20", ItemDescription = "CR Coils Grade D 1.20mm x 1250mm", Quantity = 45.000m, OpenQty = 45.000m, UnitPrice = 41111.11m }
                    }
                };

                var so2 = new SalesOrder
                {
                    Id = "SO-1002",
                    DocNum = "SO-2026-0892",
                    DocEntry = 892,
                    CardCode = "C00245",
                    CardName = "Mahindra & Mahindra Auto Division",
                    DocDate = DateTime.Today.AddDays(-1),
                    DocDueDate = DateTime.Today.AddDays(7),
                    DocTotal = 2420000.00m,
                    Currency = "INR",
                    Status = "Open",
                    Remarks = "Slit coil requirement with tight width tolerance for chassis parts",
                    Items = new List<SalesOrderItem>
                    {
                        new() { Id = "SOI-201", SalesOrderId = "SO-1002", LineNum = 1, ItemCode = "JSW-HRPO-1500-25", ItemDescription = "HRPO Coils SAPH440 2.50mm x 1500mm", Quantity = 55.000m, OpenQty = 55.000m, UnitPrice = 44000.00m }
                    }
                };

                var so3 = new SalesOrder
                {
                    Id = "SO-1003",
                    DocNum = "SO-2026-0893",
                    DocEntry = 893,
                    CardCode = "C00388",
                    CardName = "Godrej & Boyce Mfg Co Ltd",
                    DocDate = DateTime.Today,
                    DocDueDate = DateTime.Today.AddDays(14),
                    DocTotal = 980000.00m,
                    Currency = "INR",
                    Status = "Open",
                    Remarks = "Galvanized sheets for appliance refrigeration outer panels",
                    Items = new List<SalesOrderItem>
                    {
                        new() { Id = "SOI-301", SalesOrderId = "SO-1003", LineNum = 1, ItemCode = "JSW-GI-1000-08", ItemDescription = "GI Coils Zero Spangle 0.80mm x 1000mm", Quantity = 22.000m, OpenQty = 22.000m, UnitPrice = 44545.45m }
                    }
                };

                var so4 = new SalesOrder
                {
                    Id = "SO-1004",
                    DocNum = "SO-2026-0894",
                    DocEntry = 894,
                    CardCode = "C00412",
                    CardName = "Maruti Suzuki India Limited",
                    DocDate = DateTime.Today.AddDays(-3),
                    DocDueDate = DateTime.Today.AddDays(5),
                    DocTotal = 3200000.00m,
                    Currency = "INR",
                    Status = "Open",
                    Remarks = "Critical skin panel grade cold rolled coils",
                    Items = new List<SalesOrderItem>
                    {
                        new() { Id = "SOI-401", SalesOrderId = "SO-1004", LineNum = 1, ItemCode = "JSW-CR-1520-16", ItemDescription = "CR Skin Panel Grade O5 1.60mm x 1520mm", Quantity = 70.000m, OpenQty = 70.000m, UnitPrice = 45714.28m }
                    }
                };

                _salesOrders[so1.Id] = so1;
                _salesOrders[so2.Id] = so2;
                _salesOrders[so3.Id] = so3;
                _salesOrders[so4.Id] = so4;
            }

            if (_cuttingPlans.IsEmpty)
            {
                var cp1 = new CuttingPlan
                {
                    Id = "CP-101",
                    PlanCode = "CP-JSW-2610-001",
                    SalesOrderId = "SO-1001",
                    SalesOrderDocNum = "SO-2026-0891",
                    CustomerName = "Tata Motors Passenger Vehicles Ltd",
                    MotherCoilNo = "MC-JSW-2026-9921",
                    HeatNo = "HT-882901",
                    Grade = "CR EDD Grade D",
                    MotherWidth = 1250.0m,
                    MotherThickness = 1.20m,
                    MotherWeight = 24.850m,
                    Status = "Sent to SAP B1",
                    SapDocEntry = 12054,
                    SapDocNum = "PR-2026-0041",
                    SapSyncStatus = "Synced",
                    SapSyncMessage = "Successfully posted to SAP B1 Production Order DocEntry: 12054",
                    SapSyncTimestamp = DateTime.UtcNow.AddHours(-3),
                    Remarks = "Slit into 3 automotive blanks. Quality approval attached.",
                    Lines = new List<CuttingPlanLine>
                    {
                        new() { Id = "CPL-1", CuttingPlanId = "CP-101", LineNum = 1, OutputItemCode = "JSW-SLT-400", OutputDescription = "Slit Coil 400mm x 1.20mm", CutWidth = 400.0m, Quantity = 1, TargetWeight = 8.000m, Tolerance = "+/- 0.3 mm", Remarks = "Front door blank" },
                        new() { Id = "CPL-2", CuttingPlanId = "CP-101", LineNum = 2, OutputItemCode = "JSW-SLT-420", OutputDescription = "Slit Coil 420mm x 1.20mm", CutWidth = 420.0m, Quantity = 1, TargetWeight = 8.400m, Tolerance = "+/- 0.3 mm", Remarks = "Rear door blank" },
                        new() { Id = "CPL-3", CuttingPlanId = "CP-101", LineNum = 3, OutputItemCode = "JSW-SLT-410", OutputDescription = "Slit Coil 410mm x 1.20mm", CutWidth = 410.0m, Quantity = 1, TargetWeight = 8.200m, Tolerance = "+/- 0.3 mm", Remarks = "Roof bow reinforcement" }
                    }
                };
                cp1.RecalculateTotals();

                var cp2 = new CuttingPlan
                {
                    Id = "CP-102",
                    PlanCode = "CP-JSW-2610-002",
                    SalesOrderId = "SO-1002",
                    SalesOrderDocNum = "SO-2026-0892",
                    CustomerName = "Mahindra & Mahindra Auto Division",
                    MotherCoilNo = "MC-JSW-2026-4402",
                    HeatNo = "HT-993104",
                    Grade = "HRPO SAPH440",
                    MotherWidth = 1500.0m,
                    MotherThickness = 2.50m,
                    MotherWeight = 28.600m,
                    Status = "Draft",
                    SapSyncStatus = "Pending",
                    SapSyncMessage = "Draft cutting plan ready for review and SAP dispatch",
                    Remarks = "4 slits with 15mm edge trim scrap removal.",
                    Lines = new List<CuttingPlanLine>
                    {
                        new() { Id = "CPL-4", CuttingPlanId = "CP-102", LineNum = 1, OutputItemCode = "JSW-SLT-350", OutputDescription = "Chassis Longitudinal Rail Slit 350mm", CutWidth = 350.0m, Quantity = 2, TargetWeight = 13.400m, Tolerance = "+/- 0.5 mm", Remarks = "Pair cut" },
                        new() { Id = "CPL-5", CuttingPlanId = "CP-102", LineNum = 2, OutputItemCode = "JSW-SLT-400", OutputDescription = "Cross Member Stamping Slit 400mm", CutWidth = 400.0m, Quantity = 1, TargetWeight = 7.650m, Tolerance = "+/- 0.5 mm", Remarks = "Center section" },
                        new() { Id = "CPL-6", CuttingPlanId = "CP-102", LineNum = 3, OutputItemCode = "JSW-SLT-370", OutputDescription = "Suspension Bracket Slit 370mm", CutWidth = 370.0m, Quantity = 1, TargetWeight = 7.070m, Tolerance = "+/- 0.5 mm", Remarks = "Outer section" }
                    }
                };
                cp2.RecalculateTotals();

                _cuttingPlans[cp1.Id] = cp1;
                _cuttingPlans[cp2.Id] = cp2;
            }
        }
    }
}
