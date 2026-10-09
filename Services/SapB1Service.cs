using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JSWMI_CRM.Services
{
    public class SapB1Service : ISapB1Service
    {
        private readonly ILogger<SapB1Service> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISupabaseService _supabaseService;
        private SapB1Config _config;

        public SapB1Service(ILogger<SapB1Service> logger, IConfiguration configuration, ISupabaseService supabaseService)
        {
            _logger = logger;
            _configuration = configuration;
            _supabaseService = supabaseService;

            _config = new SapB1Config
            {
                ServiceLayerUrl = _configuration["SapB1:ServiceLayerUrl"] ?? "https://sapb1server.corp.jsw.in:50000/b1s/v1/",
                CompanyDB = _configuration["SapB1:CompanyDB"] ?? "JSWMI_PROD",
                UserName = _configuration["SapB1:UserName"] ?? "manager",
                Password = _configuration["SapB1:Password"] ?? "SAPB1Password#2026",
                TargetDocumentType = _configuration["SapB1:TargetDocumentType"] ?? "ProductionOrder",
                UdoName = _configuration["SapB1:UdoName"] ?? "JSW_CUTPLAN",
                SimulationMode = !bool.TryParse(_configuration["SapB1:SimulationMode"], out var sim) || sim,
                BypassSslCertificate = true
            };
        }

        public SapB1Config GetConfig() => _config;

        public void UpdateConfig(SapB1Config config)
        {
            _config = config;
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            if (_config.BypassSslCertificate)
            {
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(_config.ServiceLayerUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(15)
            };

            return client;
        }

        public async Task<(bool isSuccess, string message)> TestConnectionAsync()
        {
            if (_config.SimulationMode)
            {
                await Task.Delay(300);
                return (true, $"[SANDBOX / SIMULATION MODE] Successfully connected to SAP B1 Service Layer on {_config.CompanyDB} as {_config.UserName}.");
            }

            try
            {
                using var client = CreateHttpClient();
                var loginPayload = new
                {
                    CompanyDB = _config.CompanyDB,
                    UserName = _config.UserName,
                    Password = _config.Password
                };

                var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Login", content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, $"SAP B1 Service Layer authentication succeeded. HTTP {(int)response.StatusCode} OK.");
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    return (false, $"SAP B1 Login failed ({response.StatusCode}): {responseBody}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to SAP B1 Service Layer");
                return (false, $"Connection to SAP B1 failed: {ex.Message}. (Tip: Enable 'Simulation Mode' in Settings to test without live SAP server).");
            }
        }

        public async Task<(bool isSuccess, string message, int? docEntry, string? docNum)> PostCuttingPlanAsync(CuttingPlan plan)
        {
            var startTime = DateTime.UtcNow;
            string endpoint = _config.TargetDocumentType == "CustomUDO" ? _config.UdoName : "ProductionOrders";

            // Prepare SAP B1 Payload
            object payload;
            if (_config.TargetDocumentType == "CustomUDO")
            {
                var linesList = new List<object>();
                foreach (var line in plan.Lines)
                {
                    linesList.Add(new
                    {
                        U_LineNum = line.LineNum,
                        U_ItemCode = line.OutputItemCode,
                        U_Description = line.OutputDescription,
                        U_CutWidth = line.CutWidth,
                        U_CutLength = line.CutLength,
                        U_Quantity = line.Quantity,
                        U_TargetWeight = line.TargetWeight,
                        U_Tolerance = line.Tolerance,
                        U_Edge = line.EdgeCondition,
                        U_Remarks = line.Remarks
                    });
                }

                payload = new
                {
                    Code = plan.PlanCode,
                    Name = $"Cutting Plan {plan.PlanCode} for SO {plan.SalesOrderDocNum}",
                    U_SalesOrder = plan.SalesOrderDocNum,
                    U_Customer = plan.CustomerName,
                    U_MotherCoil = plan.MotherCoilNo,
                    U_HeatNo = plan.HeatNo,
                    U_Grade = plan.Grade,
                    U_MotherWidth = plan.MotherWidth,
                    U_MotherThick = plan.MotherThickness,
                    U_MotherWeight = plan.MotherWeight,
                    U_TotalOutputWidth = plan.TotalOutputWidth,
                    U_ScrapWidth = plan.ScrapWidth,
                    U_TotalOutputWeight = plan.TotalOutputWeight,
                    U_YieldPct = plan.YieldPercentage,
                    U_Remarks = plan.Remarks,
                    JSW_CUTPLAN_LINESCollection = linesList
                };
            }
            else
            {
                // Standard SAP B1 Production Order (Disassembly / Slitting)
                var prodLines = new List<object>
                {
                    new
                    {
                        ItemNo = string.IsNullOrWhiteSpace(plan.MotherCoilNo) ? "COIL-RAW-001" : plan.MotherCoilNo,
                        PlannedQuantity = plan.MotherWeight,
                        ItemType = "pit_Item",
                        LineType = "bopl_Item",
                        Warehouse = "01-RAW"
                    }
                };

                foreach (var line in plan.Lines)
                {
                    prodLines.Add(new
                    {
                        ItemNo = line.OutputItemCode,
                        PlannedQuantity = line.TargetWeight,
                        ItemType = "pit_Item",
                        LineType = "bopl_Item",
                        Warehouse = "02-FG",
                        Remarks = $"Width: {line.CutWidth}mm | Qty: {line.Quantity} | Tol: {line.Tolerance}"
                    });
                }

                payload = new
                {
                    ItemNo = plan.Lines.Count > 0 ? plan.Lines[0].OutputItemCode : "SLIT-COIL-FG",
                    ProductionOrderType = "bopotDisassembly", // Coil Slitting / Cutting
                    PlannedQuantity = plan.TotalOutputWeight > 0 ? plan.TotalOutputWeight : plan.MotherWeight,
                    Remarks = $"Cutting Plan: {plan.PlanCode} | SO Ref: {plan.SalesOrderDocNum} | Coil: {plan.MotherCoilNo} | Heat: {plan.HeatNo}",
                    ProductionOrderLines = prodLines,
                    UserFields = new Dictionary<string, object>
                    {
                        { "U_CuttingPlanCode", plan.PlanCode },
                        { "U_MotherCoil", plan.MotherCoilNo },
                        { "U_HeatNo", plan.HeatNo },
                        { "U_SalesOrder", plan.SalesOrderDocNum },
                        { "U_YieldPct", plan.YieldPercentage }
                    }
                };
            }

            string requestJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

            if (_config.SimulationMode)
            {
                await Task.Delay(400); // realistic latency

                int simDocEntry = new Random().Next(10500, 19999);
                string simDocNum = $"PO-{DateTime.UtcNow:yyMM}-{simDocEntry % 1000:000}";

                var simResponse = new
                {
                    DocEntry = simDocEntry,
                    DocNum = simDocNum,
                    DocumentStatus = "bost_Open",
                    ItemNo = "SLIT-COIL-FG",
                    PostingDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    Message = "Production Order successfully created in SAP Business One via Service Layer."
                };
                string responseJson = JsonSerializer.Serialize(simResponse, new JsonSerializerOptions { WriteIndented = true });

                // Log sync
                var log = new SapSyncLog
                {
                    ReferenceId = plan.Id,
                    ReferenceType = "CuttingPlan",
                    Endpoint = endpoint,
                    HttpMethod = "POST",
                    RequestPayload = requestJson,
                    ResponsePayload = responseJson,
                    HttpStatusCode = 201,
                    IsSuccess = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _supabaseService.AddSyncLogAsync(log);

                await _supabaseService.UpdateCuttingPlanSapStatusAsync(
                    plan.Id,
                    "Sent to SAP B1",
                    "Synced",
                    simDocEntry,
                    simDocNum,
                    $"[SIMULATED] Successfully posted to SAP B1 ({_config.TargetDocumentType}). DocEntry: {simDocEntry}, DocNum: {simDocNum}"
                );

                return (true, $"Cutting Plan {plan.PlanCode} successfully imported to SAP B1 as {simDocNum} (DocEntry: {simDocEntry})", simDocEntry, simDocNum);
            }

            // Real Live SAP B1 Call
            try
            {
                using var client = CreateHttpClient();

                // 1. Authenticate
                var loginPayload = new
                {
                    CompanyDB = _config.CompanyDB,
                    UserName = _config.UserName,
                    Password = _config.Password
                };

                var loginContent = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
                var loginResponse = await client.PostAsync("Login", loginContent);

                if (!loginResponse.IsSuccessStatusCode)
                {
                    var errBody = await loginResponse.Content.ReadAsStringAsync();
                    var failedLog = new SapSyncLog
                    {
                        ReferenceId = plan.Id,
                        ReferenceType = "CuttingPlan",
                        Endpoint = "Login",
                        HttpMethod = "POST",
                        RequestPayload = JsonSerializer.Serialize(loginPayload),
                        ResponsePayload = errBody,
                        HttpStatusCode = (int)loginResponse.StatusCode,
                        IsSuccess = false,
                        ErrorMessage = $"SAP B1 Authentication failed: {errBody}"
                    };
                    await _supabaseService.AddSyncLogAsync(failedLog);

                    await _supabaseService.UpdateCuttingPlanSapStatusAsync(
                        plan.Id,
                        "Sync Error",
                        "Failed",
                        null,
                        null,
                        $"SAP B1 Login failed: {errBody}"
                    );

                    return (false, $"SAP B1 Authentication failed: {errBody}", null, null);
                }

                // Forward session cookies
                if (loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                    {
                        client.DefaultRequestHeaders.Add("Cookie", cookie);
                    }
                }

                // 2. Post document
                var postContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var postResponse = await client.PostAsync(endpoint, postContent);
                var postResponseBody = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    int? docEntry = null;
                    string? docNum = null;

                    try
                    {
                        using var doc = JsonDocument.Parse(postResponseBody);
                        if (doc.RootElement.TryGetProperty("DocEntry", out var de)) docEntry = de.GetInt32();
                        if (doc.RootElement.TryGetProperty("DocNum", out var dn)) docNum = dn.ToString();
                        if (doc.RootElement.TryGetProperty("Code", out var code) && string.IsNullOrEmpty(docNum)) docNum = code.GetString();
                    }
                    catch
                    {
                        // JSON parsing fallback
                    }

                    var successLog = new SapSyncLog
                    {
                        ReferenceId = plan.Id,
                        ReferenceType = "CuttingPlan",
                        Endpoint = endpoint,
                        HttpMethod = "POST",
                        RequestPayload = requestJson,
                        ResponsePayload = postResponseBody,
                        HttpStatusCode = (int)postResponse.StatusCode,
                        IsSuccess = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _supabaseService.AddSyncLogAsync(successLog);

                    await _supabaseService.UpdateCuttingPlanSapStatusAsync(
                        plan.Id,
                        "Sent to SAP B1",
                        "Synced",
                        docEntry,
                        docNum,
                        $"Successfully created in SAP B1. DocEntry: {docEntry}, DocNum: {docNum}"
                    );

                    return (true, $"Successfully imported Cutting Plan into SAP B1 ({docNum})", docEntry, docNum);
                }
                else
                {
                    var failLog = new SapSyncLog
                    {
                        ReferenceId = plan.Id,
                        ReferenceType = "CuttingPlan",
                        Endpoint = endpoint,
                        HttpMethod = "POST",
                        RequestPayload = requestJson,
                        ResponsePayload = postResponseBody,
                        HttpStatusCode = (int)postResponse.StatusCode,
                        IsSuccess = false,
                        ErrorMessage = postResponseBody,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _supabaseService.AddSyncLogAsync(failLog);

                    await _supabaseService.UpdateCuttingPlanSapStatusAsync(
                        plan.Id,
                        "Sync Error",
                        "Failed",
                        null,
                        null,
                        $"SAP B1 Error ({postResponse.StatusCode}): {postResponseBody}"
                    );

                    return (false, $"SAP B1 Error: {postResponseBody}", null, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception posting Cutting Plan to SAP B1");
                var errorLog = new SapSyncLog
                {
                    ReferenceId = plan.Id,
                    ReferenceType = "CuttingPlan",
                    Endpoint = endpoint,
                    HttpMethod = "POST",
                    RequestPayload = requestJson,
                    ResponsePayload = ex.ToString(),
                    HttpStatusCode = 500,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    CreatedAt = DateTime.UtcNow
                };
                await _supabaseService.AddSyncLogAsync(errorLog);

                await _supabaseService.UpdateCuttingPlanSapStatusAsync(
                    plan.Id,
                    "Sync Error",
                    "Failed",
                    null,
                    null,
                    $"Network error connecting to SAP B1: {ex.Message}"
                );

                return (false, $"Network error connecting to SAP B1: {ex.Message}", null, null);
            }
        }

        public async Task<List<SalesOrder>> FetchSalesOrdersFromSapAsync()
        {
            if (_config.SimulationMode)
            {
                await Task.Delay(300);
                return await _supabaseService.GetSalesOrdersAsync();
            }

            try
            {
                using var client = CreateHttpClient();

                var loginPayload = new
                {
                    CompanyDB = _config.CompanyDB,
                    UserName = _config.UserName,
                    Password = _config.Password
                };

                var loginContent = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
                var loginResponse = await client.PostAsync("Login", loginContent);

                if (!loginResponse.IsSuccessStatusCode)
                {
                    throw new Exception("SAP B1 Login failed");
                }

                if (loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                    {
                        client.DefaultRequestHeaders.Add("Cookie", cookie);
                    }
                }

                var getResponse = await client.GetAsync("Orders?$filter=DocumentStatus eq 'bost_Open'&$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal&$top=20");
                if (getResponse.IsSuccessStatusCode)
                {
                    var content = await getResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var list = new List<SalesOrder>();

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var el in valueArray.EnumerateArray())
                        {
                            var so = new SalesOrder
                            {
                                Id = "SO-" + el.GetProperty("DocEntry").GetInt32(),
                                DocEntry = el.GetProperty("DocEntry").GetInt32(),
                                DocNum = el.GetProperty("DocNum").ToString(),
                                CardCode = el.GetProperty("CardCode").GetString() ?? "",
                                CardName = el.GetProperty("CardName").GetString() ?? "",
                                DocTotal = el.GetProperty("DocTotal").GetDecimal(),
                                Status = "Open"
                            };
                            if (el.TryGetProperty("DocDate", out var dd) && DateTime.TryParse(dd.GetString(), out var dt)) so.DocDate = dt;
                            if (el.TryGetProperty("DocDueDate", out var ddd) && DateTime.TryParse(ddd.GetString(), out var dtd)) so.DocDueDate = dtd;

                            list.Add(so);
                            await _supabaseService.SaveSalesOrderAsync(so);
                        }
                    }

                    return list;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch sales orders from SAP B1");
            }

            return await _supabaseService.GetSalesOrdersAsync();
        }
    }
}
