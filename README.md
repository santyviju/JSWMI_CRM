# JSWMI_CRM - Steel Service Center CRM & SAP Business One Integration

A high-performance Web CRM application engineered with **ASP.NET Core Razor Pages (.NET 8)** on the frontend, **Supabase (Cloud PostgreSQL)** on the backend, and an automated **SAP Business One (SAP B1) Service Layer REST API** integration pipeline for importing Cutting Plans linked to Sales Orders.

---

## 🌟 Key Features

1. **Sales Order Management (`/SalesOrders`)**:
   - Track and view customer sales orders from automotive and appliance OEMs (Tata Motors, Mahindra, Godrej, Maruti Suzuki, etc.).
   - Direct two-way sync with SAP B1 (`GET /b1s/v1/Orders`).
   - 1-click **"Plan Cuts"** action to generate a dedicated cutting schedule from any Sales Order.

2. **Cutting Plan Optimization & Builder (`/CuttingPlans/Create`)**:
   - Associates customer demand directly to raw material **Mother Coils** (Coil No, Heat No, Steel Grade, Width, Thickness, Weight).
   - **Fast Spreadsheet Import**: Upload Excel (`.xlsx`) or `.csv` cutting plans to automatically parse and populate slit strips.
   - **Downloadable Templates**: Built-in template generator for Excel and CSV formats.
   - **Real-Time Visual Slit Gauge**: Proportional visual diagram showing Mother Coil width sliced into slits with side-trim/scrap margin.
   - **Instant Calculation Engine**: Real-time evaluation of Total Output Width, Scrap Width, Total Weight, and Yield / Recovery %.

3. **SAP Business One Service Layer API Integration (`/SapIntegration`)**:
   - **Automated Document Import**: Pushes Cutting Plans as SAP B1 **Disassembly Production Orders** (`POST /b1s/v1/ProductionOrders`) with the Mother Coil as raw material and all slit specifications as finished components.
   - **Custom UDO Support**: Configurable to push as User Defined Objects (e.g. `@JSW_CUTPLAN`) if custom tables are deployed in SAP B1.
   - **Audit Logs & JSON Payload Inspector**: Full history of HTTP requests, status codes, and response payloads.
   - **Sandbox / Simulation Switch**: Toggle simulation mode to test end-to-end functionality offline without needing a live SAP B1 server connection.

4. **Cloud Database (Supabase)**:
   - Relational PostgreSQL database schema in `Database/supabase_schema.sql`.
   - In-app SQL Schema Viewer with 1-click copy for the Supabase SQL Editor.
   - Built-in resilient caching so the application runs immediately with zero external blockers.

---

## 🏗️ Project Architecture

```
JSWMI_CRM/
├── Database/
│   └── supabase_schema.sql       # PostgreSQL DDL script for Supabase
├── Models/
│   ├── SalesOrder.cs             # Sales Order & Line Item domain models
│   ├── CuttingPlan.cs            # Mother coil, cutting specs, yield logic
│   ├── CuttingPlanImportDto.cs   # Excel / CSV column mapping DTOs
│   ├── SapB1Config.cs            # Service Layer credentials & endpoints
│   ├── SupabaseConfig.cs         # Supabase Cloud URL & keys
│   └── SapSyncLog.cs             # API audit trail & JSON log model
├── Services/
│   ├── ISupabaseService.cs       # Supabase Cloud & resilient persistence
│   ├── SupabaseService.cs
│   ├── ISapB1Service.cs          # SAP B1 Service Layer HTTP client & dispatcher
│   ├── SapB1Service.cs
│   ├── ICuttingPlanImportService.cs # Excel (ClosedXML) & CSV (CsvHelper) parser
│   └── CuttingPlanImportService.cs
├── Pages/
│   ├── Index.cshtml              # Executive CRM & Operations Dashboard
│   ├── SalesOrders/              # Order list & detailed view
│   ├── CuttingPlans/             # Plan listing, builder, detail view & template download
│   ├── SapIntegration/           # Service layer status, tests & audit viewer
│   └── Settings/                 # Supabase & SAP B1 settings & SQL script
├── wwwroot/css/site.css          # JSW industrial theme & steel gauge styles
├── appsettings.json              # Connection & environment settings
└── Program.cs                    # Dependency injection & pipeline setup
```

---

## 🚀 Getting Started

### 1. Prerequisites
- **.NET 8 SDK** (Installed: `8.0.425 win-x64`)
- Any modern web browser (Edge, Chrome, Firefox)

### 2. Run the Application
Open a terminal in `d:\SantoshProjects\JSWMI_CRM` and execute:
```bash
dotnet run
```
Then navigate to:
```
https://localhost:5001  or  http://localhost:5000
```

---

## ☁️ Supabase Cloud Setup

1. Log into your [Supabase Dashboard](https://supabase.com).
2. Create a project named `JSWMI_CRM`.
3. Go to the **SQL Editor** tab in Supabase.
4. Open `Database/supabase_schema.sql` (or view it inside the CRM at `/Settings`), copy all queries, paste into the Supabase SQL editor, and click **Run**.
5. Copy your **Project URL** and **API anon key** from `Project Settings -> API`.
6. Navigate to `/Settings` in the CRM app and save your Supabase credentials.

---

## 🔌 SAP Business One Service Layer Configuration

In `appsettings.json` or under the `/Settings` page:
```json
"SapB1": {
  "ServiceLayerUrl": "https://sapb1server.corp.jsw.in:50000/b1s/v1/",
  "CompanyDB": "JSWMI_PROD",
  "UserName": "manager",
  "Password": "YOUR_PASSWORD",
  "TargetDocumentType": "ProductionOrder",
  "SimulationMode": true
}
```

- When **SimulationMode** is `true`, API calls will generate realistic simulated SAP DocEntry and DocNum records (useful for development, UAT, and staging).
- When set to `false`, the system authenticates against `POST /b1s/v1/Login` with cookie sessions and creates live documents in SAP Business One.
