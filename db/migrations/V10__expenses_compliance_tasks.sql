-- V10: Invoices, ComplianceItems, ComplianceChecks, PMTasks

CREATE TABLE IF NOT EXISTS Invoices (
  InvoiceId        BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId       BIGINT NOT NULL,
  VendorId         BIGINT NULL,
  PropertyId       BIGINT NULL,
  MaintenanceJobId BIGINT NULL,
  InvoiceNumber    VARCHAR(100) NOT NULL,
  InvoiceDate      DATE NOT NULL,
  DueDate          DATE NULL,
  Amount           DECIMAL(10,2) NOT NULL,
  GstAmount        DECIMAL(10,2) NOT NULL DEFAULT 0.00,
  StatusCode       VARCHAR(50) NOT NULL DEFAULT 'UNPAID',
  PaidDate         DATE NULL,
  Category         VARCHAR(100) NULL,
  Description      VARCHAR(255) NULL,
  CreatedAtUtc     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  INDEX IX_Invoices_Customer_Status (CustomerId, StatusCode, DueDate),
  INDEX IX_Invoices_Property (CustomerId, PropertyId),
  INDEX IX_Invoices_Vendor (CustomerId, VendorId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS InvoiceLineItems (
  LineItemId   BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId   BIGINT NOT NULL,
  InvoiceId    BIGINT NOT NULL,
  Description  VARCHAR(255) NOT NULL,
  Quantity     DECIMAL(8,2) NOT NULL DEFAULT 1.00,
  UnitPrice    DECIMAL(10,2) NOT NULL,
  LineTotal    DECIMAL(10,2) NOT NULL,
  TaxCode      VARCHAR(20) NOT NULL DEFAULT 'GST',
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (InvoiceId)  REFERENCES Invoices(InvoiceId),
  INDEX IX_LineItems_Invoice (CustomerId, InvoiceId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS ComplianceItems (
  ComplianceItemId  BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId        BIGINT NOT NULL,
  PropertyId        BIGINT NOT NULL,
  ComplianceType    VARCHAR(100) NOT NULL,
  FrequencyMonths   INT NOT NULL DEFAULT 12,
  LastCheckedDate   DATE NULL,
  NextDueDate       DATE NULL,
  StatusCode        VARCHAR(50) NOT NULL DEFAULT 'CURRENT',
  ResponsibleParty  VARCHAR(200) NULL,
  Notes             TEXT NULL,
  CreatedAtUtc      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  INDEX IX_Compliance_Customer_Status (CustomerId, StatusCode, NextDueDate),
  INDEX IX_Compliance_Property (CustomerId, PropertyId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS ComplianceChecks (
  CheckId           BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId        BIGINT NOT NULL,
  ComplianceItemId  BIGINT NOT NULL,
  PropertyId        BIGINT NOT NULL,
  CheckDate         DATE NOT NULL,
  Outcome           VARCHAR(50) NOT NULL,
  CertificateNumber VARCHAR(100) NULL,
  CertificateExpiry DATE NULL,
  PerformedBy       VARCHAR(200) NULL,
  Cost              DECIMAL(10,2) NULL,
  Notes             TEXT NULL,
  NextCheckDate     DATE NULL,
  CreatedAtUtc      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId)       REFERENCES Customers(CustomerId),
  FOREIGN KEY (ComplianceItemId) REFERENCES ComplianceItems(ComplianceItemId),
  INDEX IX_Checks_Property (CustomerId, PropertyId, CheckDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS PMTasks (
  TaskId             BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId         BIGINT NOT NULL,
  AssignedToUserId   VARCHAR(100) NULL,
  RelatedEntityType  VARCHAR(50) NULL,
  RelatedEntityId    BIGINT NULL,
  Title              VARCHAR(255) NOT NULL,
  Description        TEXT NULL,
  Priority           VARCHAR(30) NOT NULL DEFAULT 'MEDIUM',
  StatusCode         VARCHAR(50) NOT NULL DEFAULT 'OPEN',
  DueDate            DATE NULL,
  CompletedDate      DATE NULL,
  CreatedAtUtc       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  INDEX IX_Tasks_Customer_Status (CustomerId, StatusCode, DueDate),
  INDEX IX_Tasks_User (CustomerId, AssignedToUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- SEED: Invoices (20 invoices across vendors and properties)
-- ============================================================
INSERT INTO Invoices (CustomerId, VendorId, PropertyId, MaintenanceJobId, InvoiceNumber, InvoiceDate, DueDate, Amount, GstAmount, StatusCode, PaidDate, Category, Description) VALUES
-- PAID
(1, 11,  3, 11, 'INV-CLEAR-0219', '2026-01-12', '2026-01-26',  380.00, 38.00, 'PAID', '2026-01-18', 'Plumbing',    'Blocked stormwater drain — root cleared'),
(1, 12,  5, 12, 'INV-SAFE-0312',  '2026-01-16', '2026-01-30',   85.00,  8.50, 'PAID', '2026-01-22', 'Electrical',  'Faulty light switch replacement'),
(1, 13,  7, 13, 'INV-GREN-0108',  '2026-01-21', '2026-02-04',  300.00, 30.00, 'PAID', '2026-01-28', 'Landscaping', 'Garden maintenance — between tenancies'),
(1, 14,  9, 14, 'INV-FRSH-0088',  '2026-01-27', '2026-02-10',  220.00, 22.00, 'PAID', '2026-02-05', 'Painting',    'Touch-up paint walls lounge + hallway'),
(1, 15, 11, 15, 'INV-CLNS-0245',  '2026-02-06', '2026-02-20',  480.00, 48.00, 'PAID', '2026-02-14', 'Cleaning',    'Deep clean post water damage'),
(1, 18,  9,  9, 'INV-LOCK-0081',  '2026-02-24', '2026-03-10',  185.00, 18.50, 'PAID', '2026-03-01', 'Security',    'Deadbolt replacement — front door'),
-- UNPAID (within terms)
(1, 11,  1,  1, 'INV-CLEAR-0228', '2026-03-07', '2026-03-21',  340.00, 34.00, 'UNPAID', NULL, 'Plumbing',    'Burst pipe repair under kitchen sink'),
(1, 12,  2,  2, 'INV-SAFE-0331',  '2026-03-08', '2026-03-22',  275.00, 27.50, 'UNPAID', NULL, 'Electrical',  'RCD fault diagnosis and replacement'),
(1, 13, 10,  6, 'INV-GREN-0118',  '2026-03-05', '2026-03-19',  340.00, 34.00, 'UNPAID', NULL, 'Landscaping', 'Overgrown front garden clearance'),
(1, 14, 12,  7, 'INV-FRSH-0099',  '2026-03-11', '2026-03-25', 1490.00,149.00, 'UNPAID', NULL, 'Painting',    'Mould remediation + repaint bathroom'),
(1, 17,  8,  5, 'INV-TOPL-0033',  '2026-03-06', '2026-03-20',  880.00, 88.00, 'UNPAID', NULL, 'Roofing',     'Roof repair and resealing — 2nd bedroom'),
(1, 16,  4,  3, 'INV-RPFL-0071',  '2026-03-05', '2026-03-19',  590.00, 59.00, 'UNPAID', NULL, 'HVAC',        'Split system repair — refrigerant recharge'),
-- OVERDUE
(1, 19, 16,  NULL,'INV-BRSP-0201', '2026-01-28', '2026-02-11',  310.00, 31.00, 'OVERDUE', NULL, 'Electrical',  'Switchboard RCD installation'),
(1, 20, 18,  10, 'INV-BLDR-0312', '2026-02-26', '2026-03-05',  390.00, 39.00, 'OVERDUE', NULL, 'Carpentry',   'Storm-damaged fence panel replacement'),
(1, 15, 16,   9, 'INV-CLNS-0267', '2026-02-10', '2026-02-24',  820.00, 82.00, 'OVERDUE', NULL, 'Pest Control','Termite inspection and barrier treatment'),
-- DISPUTED
(1, 11,  6,  4, 'INV-CLEAR-0236', '2026-03-10', '2026-03-24', 1180.00,118.00, 'DISPUTED',NULL, 'Plumbing',    'Hot water system replacement — amount disputed');

-- ============================================================
-- SEED: Invoice Line Items
-- ============================================================
INSERT INTO InvoiceLineItems (CustomerId, InvoiceId, Description, Quantity, UnitPrice, LineTotal, TaxCode) VALUES
(1, 1, 'Labour — hydro jet drain clearing (2hrs)', 2.00, 110.00, 220.00, 'GST'),
(1, 1, 'Call-out fee', 1.00, 80.00, 80.00, 'GST'),
(1, 1, 'Root treatment chemical', 1.00, 80.00, 80.00, 'GST'),
(1, 2, 'Light switch supply + installation', 1.00, 85.00, 85.00, 'GST'),
(1, 3, 'Full garden cleanup labour (4hrs)', 4.00, 55.00, 220.00, 'GST'),
(1, 3, 'Green waste removal', 1.00, 80.00, 80.00, 'GST'),
(1, 4, 'Painting labour (3hrs)', 3.00, 55.00, 165.00, 'GST'),
(1, 4, 'Paint materials', 1.00, 55.00, 55.00, 'GST'),
(1, 5, 'Deep clean — full property (8hrs)', 8.00, 55.00, 440.00, 'GST'),
(1, 5, 'Cleaning materials', 1.00, 40.00, 40.00, 'GST'),
(1, 6, 'Deadbolt lock supply', 1.00, 95.00, 95.00, 'GST'),
(1, 6, 'Installation labour (1hr)', 1.00, 90.00, 90.00, 'GST'),
(1, 7, 'Emergency call-out (1hr)', 1.00, 120.00, 120.00, 'GST'),
(1, 7, 'Pipe repair materials', 1.00, 140.00, 140.00, 'GST'),
(1, 7, 'Labour additional (1hr)', 1.00, 80.00, 80.00, 'GST'),
(1, 8, 'RCD fault diagnosis', 1.00, 110.00, 110.00, 'GST'),
(1, 8, 'RCD replacement + certification', 1.00, 165.00, 165.00, 'GST'),
(1, 14, 'Replace fence panels x3 (supply + fit)', 3.00, 110.00, 330.00, 'GST'),
(1, 14, 'Post repair x1', 1.00, 60.00, 60.00, 'GST'),
(1, 15, 'Termite inspection', 1.00, 220.00, 220.00, 'GST'),
(1, 15, 'Chemical barrier treatment (perimeter)', 1.00, 600.00, 600.00, 'GST');

-- ============================================================
-- SEED: Compliance Items — 3 types x 18 properties
-- ============================================================
INSERT INTO ComplianceItems (CustomerId, PropertyId, ComplianceType, FrequencyMonths, LastCheckedDate, NextDueDate, StatusCode, ResponsibleParty) VALUES
-- SMOKE_ALARM (annual)
(1,  1, 'SMOKE_ALARM', 12, '2025-08-15', '2026-08-15', 'CURRENT',  'SafeWire Electrical'),
(1,  2, 'SMOKE_ALARM', 12, '2025-09-20', '2026-09-20', 'CURRENT',  'SafeWire Electrical'),
(1,  3, 'SMOKE_ALARM', 12, '2026-02-10', '2027-02-10', 'CURRENT',  'SafeWire Electrical'),
(1,  4, 'SMOKE_ALARM', 12, '2025-11-10', '2026-11-10', 'CURRENT',  'BrightSpark Electrical'),
(1,  5, 'SMOKE_ALARM', 12, '2026-01-05', '2027-01-05', 'CURRENT',  'SafeWire Electrical'),
(1,  6, 'SMOKE_ALARM', 12, '2025-07-01', '2026-07-01', 'CURRENT',  'SafeWire Electrical'),
(1,  7, 'SMOKE_ALARM', 12, '2025-06-15', '2026-06-15', 'DUE_SOON', 'SafeWire Electrical'),
(1,  8, 'SMOKE_ALARM', 12, '2025-04-10', '2026-04-10', 'DUE_SOON', 'BrightSpark Electrical'),
(1,  9, 'SMOKE_ALARM', 12, '2025-12-20', '2026-12-20', 'CURRENT',  'SafeWire Electrical'),
(1, 10, 'SMOKE_ALARM', 12, '2025-10-05', '2026-10-05', 'CURRENT',  'SafeWire Electrical'),
(1, 11, 'SMOKE_ALARM', 12, '2025-03-01', '2026-03-01', 'OVERDUE',  'SafeWire Electrical'),
(1, 12, 'SMOKE_ALARM', 12, '2025-02-14', '2026-02-14', 'OVERDUE',  'BrightSpark Electrical'),
(1, 13, 'SMOKE_ALARM', 12, '2026-01-18', '2027-01-18', 'CURRENT',  'SafeWire Electrical'),
(1, 14, 'SMOKE_ALARM', 12, '2025-08-22', '2026-08-22', 'CURRENT',  'SafeWire Electrical'),
(1, 15, 'SMOKE_ALARM', 12, '2025-05-10', '2026-05-10', 'DUE_SOON', 'BrightSpark Electrical'),
(1, 16, 'SMOKE_ALARM', 12, '2025-01-15', '2026-01-15', 'OVERDUE',  'SafeWire Electrical'),
(1, 17, 'SMOKE_ALARM', 12, '2025-11-30', '2026-11-30', 'CURRENT',  'SafeWire Electrical'),
(1, 18, 'SMOKE_ALARM', 12, '2026-02-28', '2027-02-28', 'CURRENT',  'SafeWire Electrical'),
-- RCD (bi-annual)
(1,  1, 'RCD', 24, '2025-03-10', '2027-03-10', 'CURRENT',  'SafeWire Electrical'),
(1,  2, 'RCD', 24, '2024-08-05', '2026-08-05', 'CURRENT',  'SafeWire Electrical'),
(1,  4, 'RCD', 24, '2024-06-15', '2026-06-15', 'DUE_SOON', 'BrightSpark Electrical'),
(1,  6, 'RCD', 24, '2024-12-20', '2026-12-20', 'CURRENT',  'SafeWire Electrical'),
(1,  8, 'RCD', 24, '2023-11-01', '2025-11-01', 'OVERDUE',  'BrightSpark Electrical'),
(1, 10, 'RCD', 24, '2025-01-10', '2027-01-10', 'CURRENT',  'SafeWire Electrical'),
-- GAS_CERT (bi-annual, only gas properties)
(1,  3, 'GAS_CERT', 24, '2024-09-01', '2026-09-01', 'CURRENT',  'RapidFlow HVAC'),
(1,  5, 'GAS_CERT', 24, '2024-07-15', '2026-07-15', 'CURRENT',  'RapidFlow HVAC'),
(1,  9, 'GAS_CERT', 24, '2024-04-10', '2026-04-10', 'DUE_SOON', 'RapidFlow HVAC'),
(1, 11, 'GAS_CERT', 24, '2023-12-01', '2025-12-01', 'OVERDUE',  'RapidFlow HVAC'),
(1, 13, 'GAS_CERT', 24, '2025-02-20', '2027-02-20', 'CURRENT',  'RapidFlow HVAC');

-- ============================================================
-- SEED: Compliance Checks
-- ============================================================
INSERT INTO ComplianceChecks (CustomerId, ComplianceItemId, PropertyId, CheckDate, Outcome, CertificateNumber, CertificateExpiry, PerformedBy, Cost, Notes, NextCheckDate) VALUES
(1,  1,  1, '2025-08-15', 'PASS', 'SA-2025-1001', '2026-08-15', 'James Hargreaves', 75.00, 'All smoke alarms tested functional.', '2026-08-15'),
(1,  2,  2, '2025-09-20', 'PASS', 'SA-2025-1002', '2026-09-20', 'Sarah Mitchell',   75.00, 'Battery replaced in bedroom 2 alarm.', '2026-09-20'),
(1, 11, 11, '2025-03-01', 'FAIL', NULL,            NULL,          'James Hargreaves', 75.00, 'Smoke alarm non-functional. Unit replaced on 2025-03-05.', '2026-03-01'),
(1, 12, 12, '2025-02-14', 'FAIL', NULL,            NULL,          'Sarah Mitchell',   75.00, 'Missing hardwired alarm in kitchen. Rectification order issued.', '2026-02-14'),
(1, 19,  1, '2025-03-10', 'PASS', 'RCD-2025-0301', '2027-03-10', 'SafeWire Electrical', 120.00, 'All RCD switches tested and compliant.', '2027-03-10'),
(1, 23,  8, '2023-11-01', 'PASS', 'RCD-2023-0811', '2025-11-01', 'BrightSpark Electrical', 120.00, 'RCD compliant at time of check. Due for renewal Nov 2025.', '2025-11-01'),
(1, 25,  3, '2024-09-01', 'PASS', 'GAS-2024-0301', '2026-09-01', 'RapidFlow HVAC',  95.00,  'Gas heater service complete. Certificate issued.', '2026-09-01'),
(1, 28, 11, '2023-12-01', 'FAIL', NULL,            NULL,          'RapidFlow HVAC',  95.00,  'Gas certificate expired. Service required. Tenant notified.', '2025-12-01');

-- ============================================================
-- SEED: PM Tasks — 20 tasks for manager1 and manager2
-- ============================================================
INSERT INTO PMTasks (CustomerId, AssignedToUserId, RelatedEntityType, RelatedEntityId, Title, Description, Priority, StatusCode, DueDate) VALUES
(1, 'manager1', 'Tenancy',     11, 'VCAT hearing preparation — TenancyId 11',    'Prepare evidence bundle for VCAT hearing on 15 March.', 'HIGH',   'IN_PROGRESS', '2026-03-14'),
(1, 'manager1', 'Tenancy',     12, 'Serve breach notice — TenancyId 12',          'Ensure breach notice received. Log date and recipient.', 'HIGH',   'OPEN',        '2026-03-08'),
(1, 'manager1', 'Tenancy',     14, 'Follow up arrears payment — TenancyId 14',   'Tenant promised payment by 15 March. Call to confirm.', 'HIGH',   'OPEN',        '2026-03-15'),
(1, 'manager1', 'Maintenance',  3, 'Chase HVAC owner approval — JobId 3',        'Owner has not approved $590 AC quote. Call and chase.', 'HIGH',   'OPEN',        '2026-03-07'),
(1, 'manager2', 'Maintenance',  5, 'Confirm roof repair booking — JobId 5',      'Two quotes received. Select contractor and confirm date.', 'HIGH',  'OPEN',        '2026-03-09'),
(1, 'manager2', 'Tenancy',      1, 'Confirm partial payment plan — TenancyId 1', 'Tenant made $300 payment. Discuss plan for remainder.', 'MEDIUM', 'OPEN',        '2026-03-10'),
(1, 'manager1', 'Property',     7, 'Follow up letting application — Property 7', 'Oliver Hartmann application processing. Complete ref checks.', 'MEDIUM','IN_PROGRESS','2026-03-11'),
(1, 'manager2', 'Property',    15, 'Relist Property 15 on Flatmates',            'Listing has low clicks — try Flatmates and reduce rent $20/wk.', 'MEDIUM','OPEN',  '2026-03-12'),
(1, 'manager1', 'Compliance',  12, 'Smoke alarm rectification — Property 12',    'Failed compliance check. Book SafeWire for replacement.', 'HIGH',   'OPEN',        '2026-03-10'),
(1, 'manager2', 'Compliance',  16, 'Overdue smoke alarm check — Property 16',    'Annual check overdue since Jan 2026. Book now.', 'HIGH',    'OPEN',        '2026-03-08'),
(1, 'manager1', 'Compliance',  11, 'Overdue smoke alarm + gas — Property 11',    'Both smoke alarm and gas cert overdue. Schedule combined visit.', 'URGENT','OPEN', '2026-03-07'),
(1, 'manager2', 'Compliance',   8, 'Overdue RCD check — Property 8',            'RCD certification expired Nov 2025. Book BrightSpark.', 'HIGH',   'OPEN',        '2026-03-09'),
(1, 'manager1', 'Tenancy',     16, 'Lease renewal offer — TenancyId 16',        'Lease ends Aug 2026. Draft renewal letter with 3% rent increase.', 'MEDIUM','OPEN', '2026-03-20'),
(1, 'manager2', 'Tenancy',     17, 'Lease renewal offer — TenancyId 17',        'Lease ends Sep 2026. Contact tenant re: renewal intention.', 'MEDIUM','OPEN',       '2026-03-22'),
(1, 'manager1', 'Property',    17, 'Review listing strategy — Property 17',     '79 days on market. Recommend price drop and portal refresh.', 'HIGH',  'OPEN',       '2026-03-07'),
(1, 'manager2', 'Maintenance',  6, 'Confirm landscaping completion — JobId 6',  'GreenThumb to complete front garden. Confirm done with tenant.', 'LOW', 'OPEN',      '2026-03-15'),
(1, 'manager1', 'Maintenance', 10, 'Inspect fence repair — JobId 10',           'Fence panels replaced. Schedule PM inspection to sign off.', 'LOW',  'OPEN',       '2026-03-18'),
(1, 'manager2', 'Tenancy',     13, 'Lease renewal accepted — document — TenancyId 13', 'Tenant accepted renewal. Send formal lease documents.', 'MEDIUM','IN_PROGRESS','2026-03-08'),
(1, 'manager1', NULL,          NULL,'Monthly trust account reconciliation',      'Reconcile trust account for February 2026.', 'HIGH',    'OPEN',       '2026-03-15'),
(1, 'manager2', NULL,          NULL,'Review insurance renewals',                  'Check all landlord insurance policies renew before June.', 'MEDIUM','OPEN',        '2026-03-31');
