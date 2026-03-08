-- V3: Rich seed data for all domains
-- Covers: Compliance FAIL/PASS, clear Arrears, Lease Renewals, Vendors, Maintenance, Owner Statements
-- All for CustomerId=1

-- ============================================================
-- 1. COMPLIANCE INSPECTIONS (FAIL / PASS / PENDING)
-- ============================================================
INSERT INTO Inspections (CustomerId, PropertyId, TenancyId, InspectionType, ScheduledDate, CompletedDate, ComplianceStatus, Notes, InspectorName) VALUES
-- FAIL inspections
(1,  1,  1,  'Compliance', '2025-11-10', '2025-11-10', 'FAIL',    'Smoke alarm non-functional in master bedroom. Requires immediate replacement.', 'James Hargreaves'),
(1,  2,  2,  'Compliance', '2025-11-20', '2025-11-20', 'FAIL',    'Pool fence gate latch broken — does not self-close. Safety hazard.', 'Sarah Mitchell'),
(1,  4,  4,  'Compliance', '2025-12-05', '2025-12-05', 'FAIL',    'Gas heater service overdue by 8 months. Certificate expired.', 'David Nguyen'),
(1,  6,  6,  'Compliance', '2025-12-15', '2025-12-15', 'FAIL',    'Electrical switchboard not RCD-protected. Work order raised.', 'James Hargreaves'),
(1,  8,  7,  'Compliance', '2026-01-08', '2026-01-08', 'FAIL',    'Balcony balustrade below minimum height. Rectification notice issued.', 'Sarah Mitchell'),
(1, 10,  9,  'Compliance', '2026-01-22', '2026-01-22', 'FAIL',    'No window locks on ground floor windows. Tenant security risk.', 'David Nguyen'),
(1, 12, 11,  'Compliance', '2026-02-03', '2026-02-03', 'FAIL',    'Mould in bathroom exceeds acceptable threshold. Remediation required.', 'James Hargreaves'),
(1, 14, 13,  'Compliance', '2026-02-18', '2026-02-18', 'FAIL',    'Hardwired smoke alarm missing from kitchen area. Non-compliant.', 'Sarah Mitchell'),
-- PASS inspections
(1,  3,  3,  'Compliance', '2025-10-14', '2025-10-14', 'PASS',    'All smoke alarms tested and operational. Pool fence compliant. No issues.', 'David Nguyen'),
(1,  5,  5,  'Compliance', '2025-11-01', '2025-11-01', 'PASS',    'RCD tested, gas certificate current, all windows secure.', 'James Hargreaves'),
(1,  7,  NULL,'Compliance', '2025-12-10', '2025-12-10', 'PASS',    'Vacant property inspection passed. Ready for new tenancy.', 'Sarah Mitchell'),
(1,  9,  8,  'Compliance', '2026-01-15', '2026-01-15', 'PASS',    'Annual compliance check passed. Smoke alarms and RCDs functional.', 'David Nguyen'),
(1, 11, 10,  'Compliance', '2026-02-12', '2026-02-12', 'PASS',    'Pool barrier and gate compliant. All safety items cleared.', 'James Hargreaves'),
-- PENDING (upcoming) compliance
(1, 15, 14,  'Compliance', '2026-04-01', NULL,          'PENDING', 'Annual smoke alarm & electrical compliance check scheduled.', 'Sarah Mitchell'),
(1, 16, 15,  'Compliance', '2026-04-15', NULL,          'PENDING', 'Pool fence annual compliance inspection due.', 'David Nguyen'),
(1, 17, 16,  'Compliance', '2026-05-10', NULL,          'PENDING', 'Gas heater service and compliance certificate renewal.', 'James Hargreaves'),
-- Routine & Entry/Exit inspections
(1,  1,  1,  'Routine',    '2025-09-15', '2025-09-15', 'PASS',    'Property in good condition. Minor garden maintenance recommended.', 'Sarah Mitchell'),
(1,  2,  2,  'Routine',    '2025-10-20', '2025-10-20', 'PASS',    'General condition satisfactory. No outstanding issues.', 'David Nguyen'),
(1,  3,  3,  'Routine',    '2025-11-05', '2025-11-05', 'PASS',    'Routine inspection completed. Tenant keeping property clean.', 'James Hargreaves'),
(1,  4,  4,  'Entry',      '2025-08-01', '2025-08-01', 'PASS',    'Entry condition report completed and signed by tenant.', 'Sarah Mitchell'),
(1,  5,  5,  'Exit',       '2025-12-20', '2025-12-20', 'PASS',    'Exit inspection. Bond deduction for carpet cleaning ($220).', 'David Nguyen'),
(1,  6,  6,  'Routine',    '2026-01-10', '2026-01-10', 'PASS',    'Property in excellent condition. Tenant has maintained garden well.', 'James Hargreaves'),
(1,  7,  NULL,'Routine',   '2026-03-01', NULL,          'PENDING', 'Scheduled routine inspection for vacant property.', 'Sarah Mitchell');

-- ============================================================
-- 2. ARREARS — clear unpaid rent charges (overdue)
-- ============================================================
-- Add overdue charges with PaidDate IS NULL for tenancies 1,2,4,5,8,11,12,14
-- These create a clear arrears balance that queries can find
INSERT INTO RentLedgerEntries (CustomerId, TenancyId, EntryType, TransactionDate, DueDate, PaidDate, Amount, BalanceDelta, Description, Reference) VALUES
-- TenancyId 1 — 3 weeks overdue ($614/wk)
(1, 1, 'Charge',  '2026-01-27', '2026-01-27', NULL,         614.00,  614.00, 'Weekly rent — overdue',         'CHG-2026-001'),
(1, 1, 'Charge',  '2026-02-03', '2026-02-03', NULL,         614.00,  614.00, 'Weekly rent — overdue',         'CHG-2026-002'),
(1, 1, 'Charge',  '2026-02-10', '2026-02-10', NULL,         614.00,  614.00, 'Weekly rent — overdue',         'CHG-2026-003'),
-- TenancyId 2 — 2 fortnights overdue ($480/fn)
(1, 2, 'Charge',  '2026-01-06', '2026-01-06', NULL,         480.00,  480.00, 'Fortnightly rent — overdue',    'CHG-2026-010'),
(1, 2, 'Charge',  '2026-01-20', '2026-01-20', NULL,         480.00,  480.00, 'Fortnightly rent — overdue',    'CHG-2026-011'),
-- TenancyId 4 — 1 month overdue ($920/mo)
(1, 4, 'Charge',  '2026-01-01', '2026-01-01', NULL,         920.00,  920.00, 'Monthly rent — overdue',        'CHG-2026-020'),
(1, 4, 'Charge',  '2026-02-01', '2026-02-01', NULL,         920.00,  920.00, 'Monthly rent — overdue',        'CHG-2026-021'),
-- TenancyId 5 — 3 fortnights overdue ($320/fn)
(1, 5, 'Charge',  '2026-01-05', '2026-01-05', NULL,         320.00,  320.00, 'Fortnightly rent — overdue',    'CHG-2026-030'),
(1, 5, 'Charge',  '2026-01-19', '2026-01-19', NULL,         320.00,  320.00, 'Fortnightly rent — overdue',    'CHG-2026-031'),
(1, 5, 'Charge',  '2026-02-02', '2026-02-02', NULL,         320.00,  320.00, 'Fortnightly rent — overdue',    'CHG-2026-032'),
-- TenancyId 8 — 2 weeks overdue ($559/wk)
(1, 8, 'Charge',  '2026-02-03', '2026-02-03', NULL,         559.00,  559.00, 'Weekly rent — overdue',         'CHG-2026-040'),
(1, 8, 'Charge',  '2026-02-10', '2026-02-10', NULL,         559.00,  559.00, 'Weekly rent — overdue',         'CHG-2026-041'),
-- TenancyId 11 — 1 month overdue ($750/mo) — serious arrears
(1,11, 'Charge',  '2025-12-01', '2025-12-01', NULL,         750.00,  750.00, 'Monthly rent — overdue',        'CHG-2026-050'),
(1,11, 'Charge',  '2026-01-01', '2026-01-01', NULL,         750.00,  750.00, 'Monthly rent — overdue',        'CHG-2026-051'),
(1,11, 'Charge',  '2026-02-01', '2026-02-01', NULL,         750.00,  750.00, 'Monthly rent — overdue',        'CHG-2026-052'),
(1,11, 'Charge',  '2026-03-01', '2026-03-01', NULL,         750.00,  750.00, 'Monthly rent — overdue',        'CHG-2026-053'),
-- TenancyId 12 — 4 weeks overdue ($410/wk)
(1,12, 'Charge',  '2026-01-12', '2026-01-12', NULL,         410.00,  410.00, 'Weekly rent — overdue',         'CHG-2026-060'),
(1,12, 'Charge',  '2026-01-19', '2026-01-19', NULL,         410.00,  410.00, 'Weekly rent — overdue',         'CHG-2026-061'),
(1,12, 'Charge',  '2026-01-26', '2026-01-26', NULL,         410.00,  410.00, 'Weekly rent — overdue',         'CHG-2026-062'),
(1,12, 'Charge',  '2026-02-02', '2026-02-02', NULL,         410.00,  410.00, 'Weekly rent — overdue',         'CHG-2026-063'),
-- TenancyId 14 — 2 months overdue ($680/mo)
(1,14, 'Charge',  '2026-01-01', '2026-01-01', NULL,         680.00,  680.00, 'Monthly rent — overdue',        'CHG-2026-070'),
(1,14, 'Charge',  '2026-02-01', '2026-02-01', NULL,         680.00,  680.00, 'Monthly rent — overdue',        'CHG-2026-071'),
-- Partial payments (receipts that don't fully cover charges)
(1, 1, 'Receipt', '2026-02-15', '2026-02-15', '2026-02-15', 300.00, -300.00, 'Partial rent payment received', 'RCP-2026-001'),
(1, 4, 'Receipt', '2026-02-20', '2026-02-20', '2026-02-20', 500.00, -500.00, 'Partial rent payment received', 'RCP-2026-002'),
(1,11, 'Receipt', '2026-02-28', '2026-02-28', '2026-02-28', 400.00, -400.00, 'Partial rent payment received', 'RCP-2026-003');

-- ============================================================
-- 3. MORE LEASE RENEWALS — spread across future months
-- ============================================================
-- Update some existing ACTIVE tenancies to have upcoming LeaseEndDate
UPDATE Tenancies SET LeaseEndDate = '2026-04-30' WHERE TenancyId = 13 AND CustomerId = 1;
UPDATE Tenancies SET LeaseEndDate = '2026-05-31' WHERE TenancyId = 14 AND CustomerId = 1;
UPDATE Tenancies SET LeaseEndDate = '2026-06-30' WHERE TenancyId = 15 AND CustomerId = 1;
UPDATE Tenancies SET LeaseEndDate = '2026-07-31' WHERE TenancyId = 16 AND CustomerId = 1;
UPDATE Tenancies SET LeaseEndDate = '2026-08-31' WHERE TenancyId = 17 AND CustomerId = 1;
UPDATE Tenancies SET LeaseEndDate = '2026-09-30' WHERE TenancyId = 11 AND CustomerId = 1;
UPDATE Tenancies SET LeaseEndDate = '2026-10-31' WHERE TenancyId = 12 AND CustomerId = 1;

-- ============================================================
-- 4. MORE VENDORS
-- ============================================================
INSERT INTO Vendors (CustomerId, VendorName, Category, Abn, Phone, Email, IsActive) VALUES
(1, 'ClearDrain Plumbing Co',      'Plumbing',     '11 222 333 444', '0411 100 200', 'jobs@cleardrain.com.au',    1),
(1, 'SafeWire Electrical',         'Electrical',   '22 333 444 555', '0422 300 400', 'service@safewire.com.au',   1),
(1, 'GreenThumb Landscaping',      'Landscaping',  '33 444 555 666', '0433 500 600', 'hello@greenthumb.com.au',   1),
(1, 'FreshCoat Painting',          'Painting',     '44 555 666 777', '0444 700 800', 'quotes@freshcoat.com.au',   1),
(1, 'TotalClean Property Services','Cleaning',     '55 666 777 888', '0455 900 100', 'book@totalclean.com.au',    1),
(1, 'CoolAir HVAC Solutions',      'HVAC',         '66 777 888 999', '0466 200 300', 'service@coolair.com.au',    1),
(1, 'BrickShield Roofing',         'Roofing',      '77 888 999 000', '0477 400 500', 'jobs@brickshield.com.au',   1),
(1, 'SecureLock Locksmiths',       'Security',     '88 999 000 111', '0488 600 700', 'help@securelock.com.au',    1),
(1, 'ProPest Pest Control',        'Pest Control', '99 000 111 222', '0499 800 900', 'book@propest.com.au',       1),
(1, 'SolidBase Carpentry',         'Carpentry',    '10 111 222 333', '0410 100 200', 'trade@solidbase.com.au',    1);

-- ============================================================
-- 5. MORE MAINTENANCE JOBS (with vendor assignments, varied statuses)
-- ============================================================
INSERT INTO MaintenanceJobs (CustomerId, PropertyId, TenancyId, VendorId, StatusCode, Priority, Category, Summary, Description, OpenedAtUtc, ClosedAtUtc, EstimatedCost, ActualCost) VALUES
-- URGENT / HIGH priority open jobs
(1,  1,  1, 11, 'OPEN',       'URGENT',  'Plumbing',    'Burst pipe under kitchen sink',        'Water leaking from copper pipe joint. Tenant reports significant water pooling.',              '2026-03-05 08:00:00', NULL,                  350.00,  NULL),
(1,  2,  2, 12, 'OPEN',       'URGENT',  'Electrical',  'No power to half the property',        'RCD has tripped and will not reset. Possible fault in circuit.',                             '2026-03-06 09:30:00', NULL,                  280.00,  NULL),
(1,  4,  4, 16, 'OPEN',       'HIGH',    'HVAC',        'Air conditioning unit not cooling',    'Split system cooling unit blowing warm air. Refrigerant leak suspected.',                     '2026-03-01 14:00:00', NULL,                  600.00,  NULL),
(1,  6,  6, 11, 'OPEN',       'HIGH',    'Plumbing',    'Hot water system failure',             'Electric hot water system not heating. Unit is 12 years old.',                               '2026-03-03 10:00:00', NULL,                 1200.00,  NULL),
(1,  8,  7, 17, 'OPEN',       'HIGH',    'Roofing',     'Roof leak after heavy rain',           'Water ingress through roof in second bedroom. Ceiling plaster damaged.',                     '2026-02-28 16:00:00', NULL,                  900.00,  NULL),
(1, 10,  9, 13, 'OPEN',       'MEDIUM',  'Landscaping', 'Overgrown front garden',               'Garden significantly overgrown. Hedges blocking footpath.',                                  '2026-02-25 09:00:00', NULL,                  350.00,  NULL),
(1, 12, 11, 14, 'IN_PROGRESS','HIGH',    'Painting',    'Mould remediation and repaint',        'Mould identified in bathroom and laundry. Remediation treatment and repaint in progress.',   '2026-02-15 08:00:00', NULL,                 1500.00,  NULL),
(1, 14, 13, 18, 'IN_PROGRESS','MEDIUM',  'Security',    'Replace front door deadbolt',          'Deadbolt mechanism worn and stiff. Tenant unable to lock door easily.',                      '2026-02-20 11:00:00', NULL,                  180.00,  NULL),
(1, 16, 15, 19, 'IN_PROGRESS','MEDIUM',  'Pest Control','Termite inspection and treatment',     'Evidence of termite activity near front fence. Full inspection ordered.',                     '2026-02-18 09:00:00', NULL,                  850.00,  NULL),
(1, 18, NULL,20,'IN_PROGRESS','LOW',     'Carpentry',   'Repair fence panels',                  'Three fence panels damaged by recent storm. Repairs underway.',                              '2026-02-22 14:00:00', NULL,                  400.00,  NULL),
-- COMPLETED jobs
(1,  3,  3, 11, 'COMPLETED',  'HIGH',    'Plumbing',    'Blocked stormwater drain',             'Drain cleared. Root intrusion found and treated.',                                           '2026-01-10 09:00:00', '2026-01-11 16:00:00', 420.00,  380.00),
(1,  5,  5, 12, 'COMPLETED',  'MEDIUM',  'Electrical',  'Faulty light switch replacement',      'Living room switch replaced. All lights tested and working.',                                '2026-01-15 10:00:00', '2026-01-15 14:00:00',  90.00,   85.00),
(1,  7, NULL,13, 'COMPLETED', 'LOW',     'Landscaping', 'Garden maintenance between tenancies', 'Full garden clean up. Hedges trimmed, lawn mowed, weeds removed.',                          '2026-01-20 08:00:00', '2026-01-20 17:00:00', 300.00,  300.00),
(1,  9,  8, 14, 'COMPLETED',  'MEDIUM',  'Painting',    'Touch up paint to walls',              'Scuffs and marks painted over in lounge and hallway.',                                      '2026-01-25 09:00:00', '2026-01-25 15:00:00', 250.00,  220.00),
(1, 11, 10, 15, 'COMPLETED',  'HIGH',    'Cleaning',    'Deep clean after water damage',        'Professional clean completed post leak repair.',                                             '2026-02-05 08:00:00', '2026-02-05 17:00:00', 500.00,  480.00),
-- CANCELLED jobs
(1, 13, 12, NULL,'CANCELLED', 'LOW',     'Landscaping', 'Install garden irrigation',            'Tenant requested irrigation system. Cancelled — tenant vacating.',                           '2026-01-05 09:00:00', '2026-01-08 10:00:00', 800.00,  NULL);

-- ============================================================
-- 6. MORE OWNER STATEMENTS (recent quarters)
-- ============================================================
INSERT INTO OwnerStatements (CustomerId, OwnerId, PeriodStart, PeriodEnd, GrossIncome, Expenses, ManagementFees, NetPayout) VALUES
-- Q1 2026 statements
(1, 1, '2026-01-01', '2026-03-31',  4800.00,  320.00, 384.00,  4096.00),
(1, 2, '2026-01-01', '2026-03-31',  3600.00,  180.00, 288.00,  3132.00),
(1, 3, '2026-01-01', '2026-03-31',  5200.00,  450.00, 416.00,  4334.00),
(1, 4, '2026-01-01', '2026-03-31',  7800.00,  620.00, 624.00,  6556.00),
(1, 5, '2026-01-01', '2026-03-31',  3200.00,  250.00, 256.00,  2694.00),
(1, 6, '2026-01-01', '2026-03-31',  6400.00,  890.00, 512.00,  4998.00),
(1, 7, '2026-01-01', '2026-03-31',  4200.00,  180.00, 336.00,  3684.00),
(1, 8, '2026-01-01', '2026-03-31',  5600.00,  400.00, 448.00,  4752.00),
(1, 9, '2026-01-01', '2026-03-31',  3800.00,  210.00, 304.00,  3286.00),
(1,10, '2026-01-01', '2026-03-31',  4400.00,  320.00, 352.00,  3728.00),
-- Q4 2025 statements
(1, 1, '2025-10-01', '2025-12-31',  4800.00,  280.00, 384.00,  4136.00),
(1, 2, '2025-10-01', '2025-12-31',  3600.00,  150.00, 288.00,  3162.00),
(1, 3, '2025-10-01', '2025-12-31',  5200.00,  390.00, 416.00,  4394.00),
(1, 4, '2025-10-01', '2025-12-31',  7800.00,  740.00, 624.00,  6436.00),
(1, 5, '2025-10-01', '2025-12-31',  3200.00,  200.00, 256.00,  2744.00);
