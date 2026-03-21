-- V8: Maintenance Workflow — Quote/Approval/Invoice lifecycle
-- Extends MaintenanceJobs, adds MaintenanceQuotes table

ALTER TABLE MaintenanceJobs
  ADD COLUMN QuoteAmount          DECIMAL(10,2) NULL AFTER ActualCost,
  ADD COLUMN QuoteReceivedDate    DATE NULL AFTER QuoteAmount,
  ADD COLUMN QuoteApprovedDate    DATE NULL AFTER QuoteReceivedDate,
  ADD COLUMN QuoteApprovedByUser  VARCHAR(100) NULL AFTER QuoteApprovedDate,
  ADD COLUMN InvoiceNumber        VARCHAR(100) NULL AFTER QuoteApprovedByUser,
  ADD COLUMN InvoiceDate          DATE NULL AFTER InvoiceNumber,
  ADD COLUMN InvoiceAmount        DECIMAL(10,2) NULL AFTER InvoiceDate,
  ADD COLUMN InvoicePaidDate      DATE NULL AFTER InvoiceAmount,
  ADD COLUMN WorkOrderNumber      VARCHAR(100) NULL AFTER InvoicePaidDate,
  ADD COLUMN ScheduledDate        DATE NULL AFTER WorkOrderNumber,
  ADD COLUMN TenantNotifiedDate   DATE NULL AFTER ScheduledDate;

CREATE TABLE IF NOT EXISTS MaintenanceQuotes (
  QuoteId          BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId       BIGINT NOT NULL,
  MaintenanceJobId BIGINT NOT NULL,
  VendorId         BIGINT NOT NULL,
  QuoteDate        DATE NOT NULL,
  QuoteAmount      DECIMAL(10,2) NOT NULL,
  StatusCode       VARCHAR(50) NOT NULL DEFAULT 'PENDING',
  Notes            TEXT NULL,
  CreatedAtUtc     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId)       REFERENCES Customers(CustomerId),
  FOREIGN KEY (MaintenanceJobId) REFERENCES MaintenanceJobs(MaintenanceJobId),
  FOREIGN KEY (VendorId)         REFERENCES Vendors(VendorId),
  INDEX IX_Quotes_Job (CustomerId, MaintenanceJobId),
  INDEX IX_Quotes_Status (CustomerId, StatusCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- SEED: Update existing open/in-progress jobs with workflow data
-- MaintenanceJobIds 1-10 are OPEN/IN_PROGRESS from V3
-- ============================================================
UPDATE MaintenanceJobs SET
  WorkOrderNumber = 'WO-2026-001', ScheduledDate = '2026-03-07', TenantNotifiedDate = '2026-03-05',
  QuoteAmount = 340.00, QuoteReceivedDate = '2026-03-06'
WHERE MaintenanceJobId = 1 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  WorkOrderNumber = 'WO-2026-002', ScheduledDate = '2026-03-08', TenantNotifiedDate = '2026-03-06',
  QuoteAmount = 275.00, QuoteReceivedDate = '2026-03-07', QuoteApprovedDate = '2026-03-07', QuoteApprovedByUser = 'manager1'
WHERE MaintenanceJobId = 2 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  WorkOrderNumber = 'WO-2026-003', ScheduledDate = '2026-03-05', TenantNotifiedDate = '2026-03-03',
  QuoteAmount = 590.00, QuoteReceivedDate = '2026-03-04'
WHERE MaintenanceJobId = 3 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  WorkOrderNumber = 'WO-2026-004', ScheduledDate = '2026-03-06', TenantNotifiedDate = '2026-03-04',
  QuoteAmount = 1180.00, QuoteReceivedDate = '2026-03-05', QuoteApprovedDate = '2026-03-05', QuoteApprovedByUser = 'manager1'
WHERE MaintenanceJobId = 4 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  WorkOrderNumber = 'WO-2026-005', ScheduledDate = '2026-03-10', TenantNotifiedDate = '2026-03-02'
WHERE MaintenanceJobId = 5 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  WorkOrderNumber = 'WO-2026-008', ScheduledDate = '2026-02-20',
  QuoteAmount = 1450.00, QuoteReceivedDate = '2026-02-17', QuoteApprovedDate = '2026-02-18', QuoteApprovedByUser = 'manager2',
  InvoiceNumber = 'INV-METRO-0442', InvoiceDate = '2026-03-10', InvoiceAmount = 1490.00
WHERE MaintenanceJobId = 8 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  WorkOrderNumber = 'WO-2026-009', ScheduledDate = '2026-02-22',
  QuoteAmount = 175.00, QuoteReceivedDate = '2026-02-21', QuoteApprovedDate = '2026-02-21', QuoteApprovedByUser = 'manager1',
  InvoiceNumber = 'INV-LOCK-0081', InvoiceDate = '2026-02-24', InvoiceAmount = 185.00, InvoicePaidDate = '2026-03-01'
WHERE MaintenanceJobId = 9 AND CustomerId = 1;

-- Update completed jobs with invoice data
UPDATE MaintenanceJobs SET
  InvoiceNumber = 'INV-CLEAR-0219', InvoiceDate = '2026-01-12', InvoiceAmount = 380.00, InvoicePaidDate = '2026-01-18',
  WorkOrderNumber = 'WO-2026-C01'
WHERE MaintenanceJobId = 11 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  InvoiceNumber = 'INV-SAFE-0312', InvoiceDate = '2026-01-16', InvoiceAmount = 85.00, InvoicePaidDate = '2026-01-22',
  WorkOrderNumber = 'WO-2026-C02'
WHERE MaintenanceJobId = 12 AND CustomerId = 1;

UPDATE MaintenanceJobs SET
  InvoiceNumber = 'INV-GREN-0108', InvoiceDate = '2026-01-21', InvoiceAmount = 300.00, InvoicePaidDate = '2026-01-28',
  WorkOrderNumber = 'WO-2026-C03'
WHERE MaintenanceJobId = 13 AND CustomerId = 1;

-- ============================================================
-- SEED: Multiple quotes for open jobs (competitive quoting)
-- ============================================================
INSERT INTO MaintenanceQuotes (CustomerId, MaintenanceJobId, VendorId, QuoteDate, QuoteAmount, StatusCode, Notes) VALUES
-- Job 1: Burst pipe — 2 plumbers quoted, cheaper accepted
(1, 1, 11, '2026-03-06', 340.00, 'ACCEPTED', 'ClearDrain quote. Materials + 2hr labour. Accepted.'),
(1, 1, 16, '2026-03-06', 420.00, 'DECLINED',  'RapidFlow quote — higher call-out fee. Not accepted.'),
-- Job 2: Electrical — 2 electricians, both quoted
(1, 2, 12, '2026-03-07', 275.00, 'ACCEPTED', 'SafeWire Electrical. RCD fault diagnosis + replacement. Accepted.'),
(1, 2, 19, '2026-03-07', 310.00, 'DECLINED',  'Bright Spark quote. Higher materials cost. Not accepted.'),
-- Job 3: HVAC — only one quote so far
(1, 3, 16, '2026-03-04', 590.00, 'PENDING',  'RapidFlow HVAC quote. Awaiting owner approval before proceeding.'),
-- Job 4: Hot water — approved and in progress
(1, 4, 11, '2026-03-05', 1180.00, 'ACCEPTED', 'ClearDrain Plumbing. Replace 315L HWS. Approved by owner.'),
-- Job 5: Roofing — awaiting quotes
(1, 5, 17, '2026-03-03', 880.00, 'PENDING',  'TopLine Roofing. Repair and reseal 3m x 1m area. Pending owner approval.'),
(1, 5, 20, '2026-03-04', 750.00, 'PENDING',  'BuildRight Constructions competing quote. Also pending.'),
-- Job 6: Landscaping — single quote
(1, 6, 13, '2026-02-26', 340.00, 'ACCEPTED', 'GreenThumb quote. Full front garden clean-up. Accepted.'),
-- Job 7: Mould remediation — approved
(1, 7, 14, '2026-02-16', 1450.00, 'ACCEPTED', 'FreshCoat Painting. Mould treatment + 2-room repaint. Approved.'),
-- Job 8: Security — completed with invoice
(1, 8, 18, '2026-02-21', 175.00, 'ACCEPTED', 'LockSmart Security. Deadbolt replacement inc. 2 keys. Completed.'),
-- Job 9: Termite — 2 competing quotes
(1, 9, 15, '2026-02-19', 820.00, 'ACCEPTED', 'CleanSpace Cleaning/Pest. Full termite barrier treatment. Accepted.'),
(1, 9, 20, '2026-02-19', 950.00, 'DECLINED',  'BuildRight Constructions competing pest quote. Not accepted.'),
-- Job 10: Fence panels — carpentry
(1, 10, 20, '2026-02-23', 390.00, 'ACCEPTED', 'BuildRight Constructions. Replace 3 fence panels. Accepted.');
