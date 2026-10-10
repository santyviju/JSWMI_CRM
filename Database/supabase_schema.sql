-- ==============================================================================
-- JSWMI_CRM: Supabase PostgreSQL Schema
-- Project: JSWMI CRM - Cutting Plan & SAP Business One Integration
-- ==============================================================================

-- 1. Sales Orders Table
CREATE TABLE IF NOT EXISTS sales_orders (
    id VARCHAR(64) PRIMARY KEY,
    doc_num VARCHAR(50) NOT NULL UNIQUE,
    doc_entry INT,
    card_code VARCHAR(50) NOT NULL,
    card_name VARCHAR(200) NOT NULL,
    doc_date DATE NOT NULL,
    doc_due_date DATE NOT NULL,
    doc_total NUMERIC(18, 2) DEFAULT 0,
    currency VARCHAR(10) DEFAULT 'INR',
    status VARCHAR(30) DEFAULT 'Open',
    remarks TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 2. Sales Order Items Table
CREATE TABLE IF NOT EXISTS sales_order_items (
    id VARCHAR(64) PRIMARY KEY,
    sales_order_id VARCHAR(64) NOT NULL REFERENCES sales_orders(id) ON DELETE CASCADE,
    line_num INT NOT NULL,
    item_code VARCHAR(50) NOT NULL,
    item_description VARCHAR(250) NOT NULL,
    quantity NUMERIC(18, 3) NOT NULL,
    uom VARCHAR(20) DEFAULT 'MT',
    open_qty NUMERIC(18, 3) NOT NULL,
    unit_price NUMERIC(18, 2) DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 3. Cutting Plans Table
CREATE TABLE IF NOT EXISTS cutting_plans (
    id VARCHAR(64) PRIMARY KEY,
    plan_code VARCHAR(50) NOT NULL UNIQUE,
    sales_order_id VARCHAR(64) REFERENCES sales_orders(id) ON DELETE SET NULL,
    sales_order_doc_num VARCHAR(50) NOT NULL,
    customer_name VARCHAR(200) NOT NULL,
    mother_coil_no VARCHAR(100) NOT NULL,
    heat_no VARCHAR(100) NOT NULL,
    grade VARCHAR(100) NOT NULL,
    mother_width NUMERIC(12, 2) NOT NULL,      -- in mm
    mother_thickness NUMERIC(12, 2) NOT NULL,  -- in mm
    mother_weight NUMERIC(12, 3) NOT NULL,     -- in MT
    total_output_width NUMERIC(12, 2) DEFAULT 0,
    scrap_width NUMERIC(12, 2) DEFAULT 0,
    total_output_weight NUMERIC(12, 3) DEFAULT 0,
    yield_percentage NUMERIC(6, 2) DEFAULT 0,
    status VARCHAR(50) DEFAULT 'Draft',        -- Draft, Validated, Sent to SAP B1, Sync Error
    sap_doc_entry INT,
    sap_doc_num VARCHAR(50),
    sap_sync_status VARCHAR(50) DEFAULT 'Pending', -- Pending, Synced, Failed
    sap_sync_message TEXT,
    sap_sync_timestamp TIMESTAMPTZ,
    remarks TEXT,
    created_by VARCHAR(100) DEFAULT 'Planner',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 4. Cutting Plan Line Items (Slits / Sheets)
CREATE TABLE IF NOT EXISTS cutting_plan_lines (
    id VARCHAR(64) PRIMARY KEY,
    cutting_plan_id VARCHAR(64) NOT NULL REFERENCES cutting_plans(id) ON DELETE CASCADE,
    line_num INT NOT NULL,
    output_item_code VARCHAR(50) NOT NULL,
    output_description VARCHAR(250) NOT NULL,
    cut_width NUMERIC(12, 2) NOT NULL,         -- in mm
    cut_length NUMERIC(12, 2) DEFAULT 0,       -- in mm (0 for coil slitting)
    quantity INT NOT NULL DEFAULT 1,           -- number of slits / cuts
    target_weight NUMERIC(12, 3) NOT NULL,     -- in MT
    tolerance VARCHAR(50) DEFAULT '+/- 0.5 mm',
    edge_condition VARCHAR(50) DEFAULT 'Slit Edge',
    remarks VARCHAR(250),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 5. SAP B1 Sync History Logs
CREATE TABLE IF NOT EXISTS sap_sync_logs (
    id VARCHAR(64) PRIMARY KEY,
    reference_id VARCHAR(64) NOT NULL,
    reference_type VARCHAR(50) NOT NULL,       -- CuttingPlan, SalesOrder
    endpoint VARCHAR(250) NOT NULL,
    http_method VARCHAR(10) NOT NULL,
    request_payload TEXT,
    response_payload TEXT,
    http_status_code INT,
    is_success BOOLEAN NOT NULL DEFAULT FALSE,
    error_message TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Indexes for optimal lookup performance
CREATE INDEX IF NOT EXISTS idx_so_doc_num ON sales_orders(doc_num);
CREATE INDEX IF NOT EXISTS idx_cp_sales_order ON cutting_plans(sales_order_id);
CREATE INDEX IF NOT EXISTS idx_cp_status ON cutting_plans(status);
CREATE INDEX IF NOT EXISTS idx_cp_mother_coil ON cutting_plans(mother_coil_no);
CREATE INDEX IF NOT EXISTS idx_sync_reference ON sap_sync_logs(reference_id);

-- Sample Initial Data (JSW Steel Processing Sales Orders)
INSERT INTO sales_orders (id, doc_num, doc_entry, card_code, card_name, doc_date, doc_due_date, doc_total, status, remarks)
VALUES 
('SO-1001', 'SO-2026-0891', 891, 'C00102', 'Tata Motors Passenger Vehicles Ltd', CURRENT_DATE - INTERVAL '2 days', CURRENT_DATE + INTERVAL '10 days', 1850000.00, 'Open', 'High tensile CR steel required for automotive stamping'),
('SO-1002', 'SO-2026-0892', 892, 'C00245', 'Mahindra & Mahindra Auto Division', CURRENT_DATE - INTERVAL '1 day', CURRENT_DATE + INTERVAL '7 days', 2420000.00, 'Open', 'Slit coil requirement with tight width tolerance'),
('SO-1003', 'SO-2026-0893', 893, 'C00388', 'Godrej & Boyce Mfg Co Ltd', CURRENT_DATE, CURRENT_DATE + INTERVAL '14 days', 980000.00, 'Open', 'Galvanized sheets for appliance outer panels')
ON CONFLICT (id) DO NOTHING;

INSERT INTO sales_order_items (id, sales_order_id, line_num, item_code, item_description, quantity, uom, open_qty, unit_price)
VALUES
('SOI-1', 'SO-1001', 1, 'JSW-CR-1250-20', 'CR Coils Grade D 1.20mm x 1250mm', 45.000, 'MT', 45.000, 41111.11),
('SOI-2', 'SO-1002', 1, 'JSW-HRPO-1500-25', 'HRPO Coils SAPH440 2.50mm x 1500mm', 55.000, 'MT', 55.000, 44000.00),
('SOI-3', 'SO-1003', 1, 'JSW-GI-1000-08', 'GI Coils Zero Spangle 0.80mm x 1000mm', 22.000, 'MT', 22.000, 44545.45)
ON CONFLICT (id) DO NOTHING;

-- 6. Dynamic Branches & Processing Locations
CREATE TABLE IF NOT EXISTS locations (
    id VARCHAR(64) PRIMARY KEY,
    code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(150) NOT NULL,
    city VARCHAR(100),
    state VARCHAR(100),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 7. User Profiles with Role & Location Assignment
CREATE TABLE IF NOT EXISTS user_profiles (
    user_id VARCHAR(64) PRIMARY KEY,
    email VARCHAR(200) NOT NULL,
    full_name VARCHAR(150),
    role VARCHAR(50) DEFAULT 'Planner',      -- Admin, Planner, Supervisor
    location_id VARCHAR(64) REFERENCES locations(id) ON DELETE SET NULL,
    location_name VARCHAR(150),
    phone_number VARCHAR(50),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Alter existing tables to associate Location and Auditor
ALTER TABLE sales_orders ADD COLUMN IF NOT EXISTS location_id VARCHAR(64) REFERENCES locations(id) ON DELETE SET NULL;
ALTER TABLE sales_orders ADD COLUMN IF NOT EXISTS location_name VARCHAR(150);
ALTER TABLE sales_orders ADD COLUMN IF NOT EXISTS created_by_user_id VARCHAR(64);
ALTER TABLE sales_orders ADD COLUMN IF NOT EXISTS created_by_name VARCHAR(150);

ALTER TABLE cutting_plans ADD COLUMN IF NOT EXISTS location_id VARCHAR(64) REFERENCES locations(id) ON DELETE SET NULL;
ALTER TABLE cutting_plans ADD COLUMN IF NOT EXISTS location_name VARCHAR(150);
ALTER TABLE cutting_plans ADD COLUMN IF NOT EXISTS prepared_by_user_id VARCHAR(64);
ALTER TABLE cutting_plans ADD COLUMN IF NOT EXISTS prepared_by_name VARCHAR(150);

-- Initial 4 JSW Steel Service Center Locations
INSERT INTO locations (id, code, name, city, state, is_active)
VALUES
('LOC-VIJ', 'VIJ', 'JSW Vijayanagar Processing Center', 'Toranagallu', 'Karnataka', TRUE),
('LOC-BLR', 'BLR', 'JSW Bellary Service Center', 'Bellary', 'Karnataka', TRUE),
('LOC-MUM', 'MUM', 'JSW Kalamboli Coil Service Center', 'Navi Mumbai', 'Maharashtra', TRUE),
('LOC-PUN', 'PUN', 'JSW Chakan Automotive Service Center', 'Pune', 'Maharashtra', TRUE)
ON CONFLICT (code) DO NOTHING;

