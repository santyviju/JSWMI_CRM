using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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
            var results = new List<string>();
            bool dbSuccess = false;
            bool restSuccess = false;

            // 1. Test PostgreSQL Direct / Pooler Connection String
            if (!string.IsNullOrWhiteSpace(_config.ConnectionString) && !_config.ConnectionString.Contains("your_password"))
            {
                try
                {
                    await using var conn = new NpgsqlConnection(_config.ConnectionString);
                    await conn.OpenAsync();
                    await using var cmd = new NpgsqlCommand("SELECT version();", conn);
                    var version = await cmd.ExecuteScalarAsync();
                    dbSuccess = true;
                    results.Add("PostgreSQL Database: Connected successfully via Supabase Pooler.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to connect to Supabase PostgreSQL database");
                    results.Add($"PostgreSQL Database Error: {ex.Message}");
                }
            }

            // 2. Test Supabase Cloud REST API Endpoint
            if (!string.IsNullOrWhiteSpace(_config.Url) && !_config.Url.Contains("your-project") &&
                !string.IsNullOrWhiteSpace(_config.Key) && !_config.Key.Contains("..."))
            {
                try
                {
                    using var client = new HttpClient();
                    client.BaseAddress = new Uri(_config.Url);
                    client.DefaultRequestHeaders.Add("apikey", _config.Key);
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.Key}");

                    var response = await client.GetAsync("rest/v1/");
                    if (response.IsSuccessStatusCode)
                    {
                        restSuccess = true;
                        results.Add($"REST API: Connected successfully ({response.StatusCode}).");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        results.Add("REST API: 401 Unauthorized (Note: 'Key' must be the project 'anon' or 'service_role' JWT key, not a personal access token 'sbp_').");
                    }
                    else
                    {
                        results.Add($"REST API: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to Supabase REST endpoint");
                    results.Add($"REST API Error: {ex.Message}");
                }
            }
            else
            {
                results.Add("REST API: URL or Key is not configured.");
            }

            bool overallSuccess = dbSuccess || restSuccess;
            string combinedMessage = string.Join(" | ", results);

            return (overallSuccess, combinedMessage);
        }

        public async Task<(bool success, string? userId, string? email, string? error)> AuthenticateUserAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(_config.Url) || string.IsNullOrWhiteSpace(_config.Key))
            {
                return (false, null, null, "Supabase Cloud URL and Key must be configured to authenticate users.");
            }

            try
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(_config.Url);
                client.DefaultRequestHeaders.Add("apikey", _config.Key);

                var payload = new { email, password };
                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync("auth/v1/token?grant_type=password", jsonContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var user = doc.RootElement.GetProperty("user");
                    var id = user.GetProperty("id").GetString();
                    var userEmail = user.GetProperty("email").GetString();

                    return (true, id, userEmail, null);
                }
                else
                {
                    string errorMessage = "Invalid email or password.";
                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("msg", out var msgEl))
                        {
                            errorMessage = msgEl.GetString() ?? errorMessage;
                        }
                        else if (doc.RootElement.TryGetProperty("error_description", out var descEl))
                        {
                            errorMessage = descEl.GetString() ?? errorMessage;
                        }
                        else if (doc.RootElement.TryGetProperty("message", out var mEl))
                        {
                            errorMessage = mEl.GetString() ?? errorMessage;
                        }
                    }
                    catch
                    {
                        // Fallback to default message
                    }

                    return (false, null, null, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Supabase Auth endpoint");
                return (false, null, null, $"Authentication error: {ex.Message}");
            }
        }

        private bool HasDatabaseConnection => !string.IsNullOrWhiteSpace(_config.ConnectionString) && !_config.ConnectionString.Contains("your_password");

        private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_config.ConnectionString);

        public async Task<List<SalesOrder>> GetSalesOrdersAsync()
        {
            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();

                    var orders = new List<SalesOrder>();
                    const string sqlOrders = @"SELECT id, doc_num, doc_entry, card_code, card_name, doc_date, doc_due_date, 
                                               doc_total, currency, status, remarks, created_at, updated_at 
                                               FROM sales_orders ORDER BY doc_date DESC;";

                    await using (var cmd = new NpgsqlCommand(sqlOrders, conn))
                    await using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new SalesOrder
                            {
                                Id = reader.GetString(0),
                                DocNum = reader.GetString(1),
                                DocEntry = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                                CardCode = reader.GetString(3),
                                CardName = reader.GetString(4),
                                DocDate = reader.GetDateTime(5),
                                DocDueDate = reader.GetDateTime(6),
                                DocTotal = reader.GetDecimal(7),
                                Currency = reader.IsDBNull(8) ? "INR" : reader.GetString(8),
                                Status = reader.IsDBNull(9) ? "Open" : reader.GetString(9),
                                Remarks = reader.IsDBNull(10) ? null : reader.GetString(10),
                                CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11),
                                UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12),
                                Items = new List<SalesOrderItem>()
                            });
                        }
                    }

                    if (orders.Count > 0)
                    {
                        const string sqlItems = @"SELECT id, sales_order_id, line_num, item_code, item_description, 
                                                         quantity, uom, open_qty, unit_price 
                                                  FROM sales_order_items ORDER BY line_num;";

                        await using (var cmdItems = new NpgsqlCommand(sqlItems, conn))
                        await using (var readerItems = await cmdItems.ExecuteReaderAsync())
                        {
                            var ordersMap = orders.ToDictionary(x => x.Id);
                            while (await readerItems.ReadAsync())
                            {
                                var orderId = readerItems.GetString(1);
                                if (ordersMap.TryGetValue(orderId, out var ord))
                                {
                                    ord.Items.Add(new SalesOrderItem
                                    {
                                        Id = readerItems.GetString(0),
                                        SalesOrderId = orderId,
                                        LineNum = readerItems.GetInt32(2),
                                        ItemCode = readerItems.GetString(3),
                                        ItemDescription = readerItems.GetString(4),
                                        Quantity = readerItems.GetDecimal(5),
                                        Uom = readerItems.IsDBNull(6) ? "MT" : readerItems.GetString(6),
                                        OpenQty = readerItems.GetDecimal(7),
                                        UnitPrice = readerItems.GetDecimal(8)
                                    });
                                }
                            }
                        }

                        foreach (var o in orders) _salesOrders[o.Id] = o;
                        return orders;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load Sales Orders from live Supabase PostgreSQL database; falling back to cache.");
                }
            }

            var list = _salesOrders.Values.OrderByDescending(x => x.DocDate).ToList();
            return list;
        }

        public async Task<SalesOrder?> GetSalesOrderByIdAsync(string id)
        {
            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();

                    SalesOrder? order = null;
                    const string sqlOrder = @"SELECT id, doc_num, doc_entry, card_code, card_name, doc_date, doc_due_date, 
                                                     doc_total, currency, status, remarks, created_at, updated_at 
                                              FROM sales_orders WHERE id = @id LIMIT 1;";

                    await using (var cmd = new NpgsqlCommand(sqlOrder, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            order = new SalesOrder
                            {
                                Id = reader.GetString(0),
                                DocNum = reader.GetString(1),
                                DocEntry = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                                CardCode = reader.GetString(3),
                                CardName = reader.GetString(4),
                                DocDate = reader.GetDateTime(5),
                                DocDueDate = reader.GetDateTime(6),
                                DocTotal = reader.GetDecimal(7),
                                Currency = reader.IsDBNull(8) ? "INR" : reader.GetString(8),
                                Status = reader.IsDBNull(9) ? "Open" : reader.GetString(9),
                                Remarks = reader.IsDBNull(10) ? null : reader.GetString(10),
                                CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11),
                                UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12),
                                Items = new List<SalesOrderItem>()
                            };
                        }
                    }

                    if (order != null)
                    {
                        const string sqlItems = @"SELECT id, sales_order_id, line_num, item_code, item_description, 
                                                         quantity, uom, open_qty, unit_price 
                                                  FROM sales_order_items WHERE sales_order_id = @id ORDER BY line_num;";

                        await using (var cmdItems = new NpgsqlCommand(sqlItems, conn))
                        {
                            cmdItems.Parameters.AddWithValue("@id", id);
                            await using var readerItems = await cmdItems.ExecuteReaderAsync();
                            while (await readerItems.ReadAsync())
                            {
                                order.Items.Add(new SalesOrderItem
                                {
                                    Id = readerItems.GetString(0),
                                    SalesOrderId = id,
                                    LineNum = readerItems.GetInt32(2),
                                    ItemCode = readerItems.GetString(3),
                                    ItemDescription = readerItems.GetString(4),
                                    Quantity = readerItems.GetDecimal(5),
                                    Uom = readerItems.IsDBNull(6) ? "MT" : readerItems.GetString(6),
                                    OpenQty = readerItems.GetDecimal(7),
                                    UnitPrice = readerItems.GetDecimal(8)
                                });
                            }
                        }

                        _salesOrders[order.Id] = order;
                        return order;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load Sales Order {Id} from Supabase PostgreSQL database", id);
                }
            }

            _salesOrders.TryGetValue(id, out var cachedOrder);
            return cachedOrder;
        }

        public async Task<SalesOrder> SaveSalesOrderAsync(SalesOrder order)
        {
            if (string.IsNullOrWhiteSpace(order.Id))
            {
                order.Id = "SO-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            }

            order.UpdatedAt = DateTime.UtcNow;
            _salesOrders[order.Id] = order;

            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();

                    const string sqlUpsert = @"INSERT INTO sales_orders (id, doc_num, doc_entry, card_code, card_name, doc_date, doc_due_date, doc_total, currency, status, remarks, created_at, updated_at)
                        VALUES (@id, @doc_num, @doc_entry, @card_code, @card_name, @doc_date, @doc_due_date, @doc_total, @currency, @status, @remarks, @created_at, @updated_at)
                        ON CONFLICT (id) DO UPDATE SET
                            doc_num = EXCLUDED.doc_num,
                            doc_entry = EXCLUDED.doc_entry,
                            card_code = EXCLUDED.card_code,
                            card_name = EXCLUDED.card_name,
                            doc_date = EXCLUDED.doc_date,
                            doc_due_date = EXCLUDED.doc_due_date,
                            doc_total = EXCLUDED.doc_total,
                            currency = EXCLUDED.currency,
                            status = EXCLUDED.status,
                            remarks = EXCLUDED.remarks,
                            updated_at = EXCLUDED.updated_at;";

                    await using (var cmd = new NpgsqlCommand(sqlUpsert, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", order.Id);
                        cmd.Parameters.AddWithValue("@doc_num", order.DocNum);
                        cmd.Parameters.AddWithValue("@doc_entry", (object?)order.DocEntry ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@card_code", order.CardCode);
                        cmd.Parameters.AddWithValue("@card_name", order.CardName);
                        cmd.Parameters.AddWithValue("@doc_date", order.DocDate);
                        cmd.Parameters.AddWithValue("@doc_due_date", order.DocDueDate);
                        cmd.Parameters.AddWithValue("@doc_total", order.DocTotal);
                        cmd.Parameters.AddWithValue("@currency", order.Currency);
                        cmd.Parameters.AddWithValue("@status", order.Status);
                        cmd.Parameters.AddWithValue("@remarks", (object?)order.Remarks ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@created_at", order.CreatedAt);
                        cmd.Parameters.AddWithValue("@updated_at", order.UpdatedAt);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist Sales Order {Id} to Supabase PostgreSQL database", order.Id);
                }
            }

            return order;
        }

        public async Task<bool> DeleteSalesOrderAsync(string id)
        {
            var removed = _salesOrders.TryRemove(id, out _);
            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();
                    await using var cmd = new NpgsqlCommand("DELETE FROM sales_orders WHERE id = @id;", conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete Sales Order {Id} from Supabase PostgreSQL database", id);
                }
            }
            return removed;
        }

        public async Task<List<CuttingPlan>> GetCuttingPlansAsync()
        {
            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();

                    var plans = new List<CuttingPlan>();
                    const string sqlPlans = @"SELECT id, plan_code, sales_order_id, sales_order_doc_num, customer_name,
                                                     mother_coil_no, heat_no, grade, mother_width, mother_thickness, mother_weight,
                                                     total_output_width, scrap_width, total_output_weight, yield_percentage,
                                                     status, sap_doc_entry, sap_doc_num, sap_sync_status, sap_sync_message,
                                                     sap_sync_timestamp, remarks, created_by, created_at, updated_at
                                              FROM cutting_plans ORDER BY created_at DESC;";

                    await using (var cmd = new NpgsqlCommand(sqlPlans, conn))
                    await using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            plans.Add(new CuttingPlan
                            {
                                Id = reader.GetString(0),
                                PlanCode = reader.GetString(1),
                                SalesOrderId = reader.IsDBNull(2) ? null : reader.GetString(2),
                                SalesOrderDocNum = reader.GetString(3),
                                CustomerName = reader.GetString(4),
                                MotherCoilNo = reader.GetString(5),
                                HeatNo = reader.GetString(6),
                                Grade = reader.GetString(7),
                                MotherWidth = reader.GetDecimal(8),
                                MotherThickness = reader.GetDecimal(9),
                                MotherWeight = reader.GetDecimal(10),
                                TotalOutputWidth = reader.GetDecimal(11),
                                ScrapWidth = reader.GetDecimal(12),
                                TotalOutputWeight = reader.GetDecimal(13),
                                YieldPercentage = reader.GetDecimal(14),
                                Status = reader.IsDBNull(15) ? "Draft" : reader.GetString(15),
                                SapDocEntry = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                                SapDocNum = reader.IsDBNull(17) ? null : reader.GetString(17),
                                SapSyncStatus = reader.IsDBNull(18) ? "Pending" : reader.GetString(18),
                                SapSyncMessage = reader.IsDBNull(19) ? null : reader.GetString(19),
                                SapSyncTimestamp = reader.IsDBNull(20) ? null : reader.GetDateTime(20),
                                Remarks = reader.IsDBNull(21) ? null : reader.GetString(21),
                                CreatedBy = reader.IsDBNull(22) ? "Planner" : reader.GetString(22),
                                CreatedAt = reader.IsDBNull(23) ? DateTime.UtcNow : reader.GetDateTime(23),
                                UpdatedAt = reader.IsDBNull(24) ? DateTime.UtcNow : reader.GetDateTime(24),
                                Lines = new List<CuttingPlanLine>()
                            });
                        }
                    }

                    if (plans.Count > 0)
                    {
                        const string sqlLines = @"SELECT id, cutting_plan_id, line_num, output_item_code, output_description,
                                                         cut_width, cut_length, quantity, target_weight, tolerance, edge_condition, remarks
                                                  FROM cutting_plan_lines ORDER BY line_num;";

                        await using (var cmdLines = new NpgsqlCommand(sqlLines, conn))
                        await using (var readerLines = await cmdLines.ExecuteReaderAsync())
                        {
                            var plansMap = plans.ToDictionary(x => x.Id);
                            while (await readerLines.ReadAsync())
                            {
                                var planId = readerLines.GetString(1);
                                if (plansMap.TryGetValue(planId, out var pl))
                                {
                                    pl.Lines.Add(new CuttingPlanLine
                                    {
                                        Id = readerLines.GetString(0),
                                        CuttingPlanId = planId,
                                        LineNum = readerLines.GetInt32(2),
                                        OutputItemCode = readerLines.GetString(3),
                                        OutputDescription = readerLines.GetString(4),
                                        CutWidth = readerLines.GetDecimal(5),
                                        CutLength = readerLines.GetDecimal(6),
                                        Quantity = readerLines.GetInt32(7),
                                        TargetWeight = readerLines.GetDecimal(8),
                                        Tolerance = readerLines.IsDBNull(9) ? "+/- 0.5 mm" : readerLines.GetString(9),
                                        EdgeCondition = readerLines.IsDBNull(10) ? "Slit Edge" : readerLines.GetString(10),
                                        Remarks = readerLines.IsDBNull(11) ? null : readerLines.GetString(11)
                                    });
                                }
                            }
                        }

                        foreach (var p in plans) _cuttingPlans[p.Id] = p;
                        return plans;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load Cutting Plans from Supabase PostgreSQL database; falling back to cache.");
                }
            }

            return _cuttingPlans.Values.OrderByDescending(x => x.CreatedAt).ToList();
        }

        public async Task<CuttingPlan?> GetCuttingPlanByIdAsync(string id)
        {
            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();

                    CuttingPlan? plan = null;
                    const string sqlPlan = @"SELECT id, plan_code, sales_order_id, sales_order_doc_num, customer_name,
                                                    mother_coil_no, heat_no, grade, mother_width, mother_thickness, mother_weight,
                                                    total_output_width, scrap_width, total_output_weight, yield_percentage,
                                                    status, sap_doc_entry, sap_doc_num, sap_sync_status, sap_sync_message,
                                                    sap_sync_timestamp, remarks, created_by, created_at, updated_at
                                             FROM cutting_plans WHERE id = @id LIMIT 1;";

                    await using (var cmd = new NpgsqlCommand(sqlPlan, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            plan = new CuttingPlan
                            {
                                Id = reader.GetString(0),
                                PlanCode = reader.GetString(1),
                                SalesOrderId = reader.IsDBNull(2) ? null : reader.GetString(2),
                                SalesOrderDocNum = reader.GetString(3),
                                CustomerName = reader.GetString(4),
                                MotherCoilNo = reader.GetString(5),
                                HeatNo = reader.GetString(6),
                                Grade = reader.GetString(7),
                                MotherWidth = reader.GetDecimal(8),
                                MotherThickness = reader.GetDecimal(9),
                                MotherWeight = reader.GetDecimal(10),
                                TotalOutputWidth = reader.GetDecimal(11),
                                ScrapWidth = reader.GetDecimal(12),
                                TotalOutputWeight = reader.GetDecimal(13),
                                YieldPercentage = reader.GetDecimal(14),
                                Status = reader.IsDBNull(15) ? "Draft" : reader.GetString(15),
                                SapDocEntry = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                                SapDocNum = reader.IsDBNull(17) ? null : reader.GetString(17),
                                SapSyncStatus = reader.IsDBNull(18) ? "Pending" : reader.GetString(18),
                                SapSyncMessage = reader.IsDBNull(19) ? null : reader.GetString(19),
                                SapSyncTimestamp = reader.IsDBNull(20) ? null : reader.GetDateTime(20),
                                Remarks = reader.IsDBNull(21) ? null : reader.GetString(21),
                                CreatedBy = reader.IsDBNull(22) ? "Planner" : reader.GetString(22),
                                CreatedAt = reader.IsDBNull(23) ? DateTime.UtcNow : reader.GetDateTime(23),
                                UpdatedAt = reader.IsDBNull(24) ? DateTime.UtcNow : reader.GetDateTime(24),
                                Lines = new List<CuttingPlanLine>()
                            };
                        }
                    }

                    if (plan != null)
                    {
                        const string sqlLines = @"SELECT id, cutting_plan_id, line_num, output_item_code, output_description,
                                                         cut_width, cut_length, quantity, target_weight, tolerance, edge_condition, remarks
                                                  FROM cutting_plan_lines WHERE cutting_plan_id = @id ORDER BY line_num;";

                        await using (var cmdLines = new NpgsqlCommand(sqlLines, conn))
                        {
                            cmdLines.Parameters.AddWithValue("@id", id);
                            await using var readerLines = await cmdLines.ExecuteReaderAsync();
                            while (await readerLines.ReadAsync())
                            {
                                plan.Lines.Add(new CuttingPlanLine
                                {
                                    Id = readerLines.GetString(0),
                                    CuttingPlanId = id,
                                    LineNum = readerLines.GetInt32(2),
                                    OutputItemCode = readerLines.GetString(3),
                                    OutputDescription = readerLines.GetString(4),
                                    CutWidth = readerLines.GetDecimal(5),
                                    CutLength = readerLines.GetDecimal(6),
                                    Quantity = readerLines.GetInt32(7),
                                    TargetWeight = readerLines.GetDecimal(8),
                                    Tolerance = readerLines.IsDBNull(9) ? "+/- 0.5 mm" : readerLines.GetString(9),
                                    EdgeCondition = readerLines.IsDBNull(10) ? "Slit Edge" : readerLines.GetString(10),
                                    Remarks = readerLines.IsDBNull(11) ? null : readerLines.GetString(11)
                                });
                            }
                        }

                        _cuttingPlans[plan.Id] = plan;
                        return plan;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load Cutting Plan {Id} from Supabase PostgreSQL database", id);
                }
            }

            _cuttingPlans.TryGetValue(id, out var cachedPlan);
            return cachedPlan;
        }

        public async Task<CuttingPlan> SaveCuttingPlanAsync(CuttingPlan plan)
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

            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();
                    await using var trans = await conn.BeginTransactionAsync();

                    const string sqlUpsertPlan = @"INSERT INTO cutting_plans (
                        id, plan_code, sales_order_id, sales_order_doc_num, customer_name,
                        mother_coil_no, heat_no, grade, mother_width, mother_thickness, mother_weight,
                        total_output_width, scrap_width, total_output_weight, yield_percentage,
                        status, sap_doc_entry, sap_doc_num, sap_sync_status, sap_sync_message,
                        sap_sync_timestamp, remarks, created_by, created_at, updated_at
                    ) VALUES (
                        @id, @plan_code, @sales_order_id, @sales_order_doc_num, @customer_name,
                        @mother_coil_no, @heat_no, @grade, @mother_width, @mother_thickness, @mother_weight,
                        @total_output_width, @scrap_width, @total_output_weight, @yield_percentage,
                        @status, @sap_doc_entry, @sap_doc_num, @sap_sync_status, @sap_sync_message,
                        @sap_sync_timestamp, @remarks, @created_by, @created_at, @updated_at
                    )
                    ON CONFLICT (id) DO UPDATE SET
                        plan_code = EXCLUDED.plan_code,
                        sales_order_id = EXCLUDED.sales_order_id,
                        sales_order_doc_num = EXCLUDED.sales_order_doc_num,
                        customer_name = EXCLUDED.customer_name,
                        mother_coil_no = EXCLUDED.mother_coil_no,
                        heat_no = EXCLUDED.heat_no,
                        grade = EXCLUDED.grade,
                        mother_width = EXCLUDED.mother_width,
                        mother_thickness = EXCLUDED.mother_thickness,
                        mother_weight = EXCLUDED.mother_weight,
                        total_output_width = EXCLUDED.total_output_width,
                        scrap_width = EXCLUDED.scrap_width,
                        total_output_weight = EXCLUDED.total_output_weight,
                        yield_percentage = EXCLUDED.yield_percentage,
                        status = EXCLUDED.status,
                        sap_doc_entry = EXCLUDED.sap_doc_entry,
                        sap_doc_num = EXCLUDED.sap_doc_num,
                        sap_sync_status = EXCLUDED.sap_sync_status,
                        sap_sync_message = EXCLUDED.sap_sync_message,
                        sap_sync_timestamp = EXCLUDED.sap_sync_timestamp,
                        remarks = EXCLUDED.remarks,
                        updated_at = EXCLUDED.updated_at;";

                    await using (var cmd = new NpgsqlCommand(sqlUpsertPlan, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@id", plan.Id);
                        cmd.Parameters.AddWithValue("@plan_code", plan.PlanCode);
                        cmd.Parameters.AddWithValue("@sales_order_id", (object?)plan.SalesOrderId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sales_order_doc_num", plan.SalesOrderDocNum);
                        cmd.Parameters.AddWithValue("@customer_name", plan.CustomerName);
                        cmd.Parameters.AddWithValue("@mother_coil_no", plan.MotherCoilNo);
                        cmd.Parameters.AddWithValue("@heat_no", plan.HeatNo);
                        cmd.Parameters.AddWithValue("@grade", plan.Grade);
                        cmd.Parameters.AddWithValue("@mother_width", plan.MotherWidth);
                        cmd.Parameters.AddWithValue("@mother_thickness", plan.MotherThickness);
                        cmd.Parameters.AddWithValue("@mother_weight", plan.MotherWeight);
                        cmd.Parameters.AddWithValue("@total_output_width", plan.TotalOutputWidth);
                        cmd.Parameters.AddWithValue("@scrap_width", plan.ScrapWidth);
                        cmd.Parameters.AddWithValue("@total_output_weight", plan.TotalOutputWeight);
                        cmd.Parameters.AddWithValue("@yield_percentage", plan.YieldPercentage);
                        cmd.Parameters.AddWithValue("@status", plan.Status);
                        cmd.Parameters.AddWithValue("@sap_doc_entry", (object?)plan.SapDocEntry ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sap_doc_num", (object?)plan.SapDocNum ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sap_sync_status", plan.SapSyncStatus);
                        cmd.Parameters.AddWithValue("@sap_sync_message", (object?)plan.SapSyncMessage ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sap_sync_timestamp", (object?)plan.SapSyncTimestamp ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@remarks", (object?)plan.Remarks ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@created_by", plan.CreatedBy);
                        cmd.Parameters.AddWithValue("@created_at", plan.CreatedAt);
                        cmd.Parameters.AddWithValue("@updated_at", plan.UpdatedAt);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Replace lines
                    await using (var cmdDel = new NpgsqlCommand("DELETE FROM cutting_plan_lines WHERE cutting_plan_id = @id;", conn, trans))
                    {
                        cmdDel.Parameters.AddWithValue("@id", plan.Id);
                        await cmdDel.ExecuteNonQueryAsync();
                    }

                    if (plan.Lines.Count > 0)
                    {
                        const string sqlInsertLine = @"INSERT INTO cutting_plan_lines (
                            id, cutting_plan_id, line_num, output_item_code, output_description,
                            cut_width, cut_length, quantity, target_weight, tolerance, edge_condition, remarks
                        ) VALUES (
                            @id, @cutting_plan_id, @line_num, @output_item_code, @output_description,
                            @cut_width, @cut_length, @quantity, @target_weight, @tolerance, @edge_condition, @remarks
                        );";

                        for (int i = 0; i < plan.Lines.Count; i++)
                        {
                            var line = plan.Lines[i];
                            if (string.IsNullOrWhiteSpace(line.Id)) line.Id = Guid.NewGuid().ToString("N");
                            line.CuttingPlanId = plan.Id;
                            line.LineNum = i + 1;

                            await using var cmdLine = new NpgsqlCommand(sqlInsertLine, conn, trans);
                            cmdLine.Parameters.AddWithValue("@id", line.Id);
                            cmdLine.Parameters.AddWithValue("@cutting_plan_id", line.CuttingPlanId);
                            cmdLine.Parameters.AddWithValue("@line_num", line.LineNum);
                            cmdLine.Parameters.AddWithValue("@output_item_code", line.OutputItemCode);
                            cmdLine.Parameters.AddWithValue("@output_description", line.OutputDescription);
                            cmdLine.Parameters.AddWithValue("@cut_width", line.CutWidth);
                            cmdLine.Parameters.AddWithValue("@cut_length", line.CutLength);
                            cmdLine.Parameters.AddWithValue("@quantity", line.Quantity);
                            cmdLine.Parameters.AddWithValue("@target_weight", line.TargetWeight);
                            cmdLine.Parameters.AddWithValue("@tolerance", line.Tolerance);
                            cmdLine.Parameters.AddWithValue("@edge_condition", line.EdgeCondition);
                            cmdLine.Parameters.AddWithValue("@remarks", (object?)line.Remarks ?? DBNull.Value);
                            await cmdLine.ExecuteNonQueryAsync();
                        }
                    }

                    await trans.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist Cutting Plan {Id} to Supabase PostgreSQL database", plan.Id);
                }
            }

            return plan;
        }

        public async Task<bool> DeleteCuttingPlanAsync(string id)
        {
            var removed = _cuttingPlans.TryRemove(id, out _);
            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();
                    await using var cmd = new NpgsqlCommand("DELETE FROM cutting_plans WHERE id = @id;", conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete Cutting Plan {Id} from Supabase PostgreSQL database", id);
                }
            }
            return removed;
        }

        public async Task UpdateCuttingPlanSapStatusAsync(string id, string status, string syncStatus, int? sapDocEntry, string? sapDocNum, string? message)
        {
            DateTime now = DateTime.UtcNow;
            if (_cuttingPlans.TryGetValue(id, out var plan))
            {
                plan.Status = status;
                plan.SapSyncStatus = syncStatus;
                plan.SapDocEntry = sapDocEntry;
                plan.SapDocNum = sapDocNum;
                plan.SapSyncMessage = message;
                plan.SapSyncTimestamp = now;
                plan.UpdatedAt = now;
            }

            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();
                    const string sqlUpdate = @"UPDATE cutting_plans SET
                        status = @status,
                        sap_sync_status = @syncStatus,
                        sap_doc_entry = @sapDocEntry,
                        sap_doc_num = @sapDocNum,
                        sap_sync_message = @message,
                        sap_sync_timestamp = @timestamp,
                        updated_at = @timestamp
                    WHERE id = @id;";

                    await using var cmd = new NpgsqlCommand(sqlUpdate, conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@syncStatus", syncStatus);
                    cmd.Parameters.AddWithValue("@sapDocEntry", (object?)sapDocEntry ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@sapDocNum", (object?)sapDocNum ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@message", (object?)message ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@timestamp", now);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update SAP status for Cutting Plan {Id} in Supabase database", id);
                }
            }
        }

        public async Task<List<SapSyncLog>> GetSyncLogsAsync()
        {
            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();
                    var list = new List<SapSyncLog>();
                    const string sqlLogs = @"SELECT id, reference_id, reference_type, endpoint, http_method,
                                                    request_payload, response_payload, http_status_code, is_success,
                                                    error_message, created_at
                                             FROM sap_sync_logs ORDER BY created_at DESC LIMIT 100;";

                    await using var cmd = new NpgsqlCommand(sqlLogs, conn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        list.Add(new SapSyncLog
                        {
                            Id = reader.GetString(0),
                            ReferenceId = reader.GetString(1),
                            ReferenceType = reader.GetString(2),
                            Endpoint = reader.GetString(3),
                            HttpMethod = reader.GetString(4),
                            RequestPayload = reader.IsDBNull(5) ? null : reader.GetString(5),
                            ResponsePayload = reader.IsDBNull(6) ? null : reader.GetString(6),
                            HttpStatusCode = reader.GetInt32(7),
                            IsSuccess = reader.GetBoolean(8),
                            ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
                            CreatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10)
                        });
                    }

                    if (list.Count > 0) return list;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load sync logs from Supabase database; falling back to cache.");
                }
            }

            lock (_syncLock)
            {
                return _syncLogs.OrderByDescending(x => x.CreatedAt).Take(100).ToList();
            }
        }

        public async Task AddSyncLogAsync(SapSyncLog log)
        {
            lock (_syncLock)
            {
                _syncLogs.Insert(0, log);
                if (_syncLogs.Count > 200)
                {
                    _syncLogs.RemoveAt(_syncLogs.Count - 1);
                }
            }

            if (HasDatabaseConnection)
            {
                try
                {
                    await using var conn = CreateConnection();
                    await conn.OpenAsync();
                    const string sqlLog = @"INSERT INTO sap_sync_logs (
                        id, reference_id, reference_type, endpoint, http_method,
                        request_payload, response_payload, http_status_code, is_success, error_message, created_at
                    ) VALUES (
                        @id, @reference_id, @reference_type, @endpoint, @http_method,
                        @request_payload, @response_payload, @http_status_code, @is_success, @error_message, @created_at
                    );";

                    await using var cmd = new NpgsqlCommand(sqlLog, conn);
                    cmd.Parameters.AddWithValue("@id", log.Id);
                    cmd.Parameters.AddWithValue("@reference_id", log.ReferenceId);
                    cmd.Parameters.AddWithValue("@reference_type", log.ReferenceType);
                    cmd.Parameters.AddWithValue("@endpoint", log.Endpoint);
                    cmd.Parameters.AddWithValue("@http_method", log.HttpMethod);
                    cmd.Parameters.AddWithValue("@request_payload", (object?)log.RequestPayload ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@response_payload", (object?)log.ResponsePayload ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@http_status_code", log.HttpStatusCode);
                    cmd.Parameters.AddWithValue("@is_success", log.IsSuccess);
                    cmd.Parameters.AddWithValue("@error_message", (object?)log.ErrorMessage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@created_at", log.CreatedAt);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist SAP sync log to Supabase database");
                }
            }
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
