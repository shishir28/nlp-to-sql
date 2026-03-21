-- V9: Arrears Escalation, Trust Ledger, Management Fees

CREATE TABLE IF NOT EXISTS ArrearsEscalations (
  EscalationId          BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId            BIGINT NOT NULL,
  TenancyId             BIGINT NOT NULL,
  Stage                 VARCHAR(50) NOT NULL,
  EscalationDate        DATE NOT NULL,
  ArrearsAmountAtStage  DECIMAL(10,2) NOT NULL,
  Notes                 TEXT NULL,
  HandledByUserId       VARCHAR(100) NULL,
  CreatedAtUtc          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (TenancyId)  REFERENCES Tenancies(TenancyId),
  INDEX IX_Escalations_Customer_Tenancy (CustomerId, TenancyId, EscalationDate),
  INDEX IX_Escalations_Stage (CustomerId, Stage)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS TrustLedger (
  TrustLedgerId      BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId         BIGINT NOT NULL,
  OwnerId            BIGINT NOT NULL,
  TransactionDate    DATE NOT NULL,
  TransactionType    VARCHAR(50) NOT NULL,
  Amount             DECIMAL(12,2) NOT NULL,
  RunningBalance     DECIMAL(12,2) NOT NULL,
  Reference          VARCHAR(100) NULL,
  Description        VARCHAR(255) NULL,
  RelatedStatementId BIGINT NULL,
  CreatedAtUtc       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (OwnerId)    REFERENCES Owners(OwnerId),
  INDEX IX_Trust_Owner (CustomerId, OwnerId, TransactionDate),
  INDEX IX_Trust_Type  (CustomerId, TransactionType)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS ManagementFeeSchedules (
  ScheduleId    BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId    BIGINT NOT NULL,
  OwnerId       BIGINT NOT NULL,
  PropertyId    BIGINT NULL,
  FeeType       VARCHAR(50) NOT NULL,
  FeeValue      DECIMAL(8,4) NOT NULL,
  EffectiveFrom DATE NOT NULL,
  EffectiveTo   DATE NULL,
  Notes         VARCHAR(255) NULL,
  CreatedAtUtc  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (OwnerId)    REFERENCES Owners(OwnerId),
  INDEX IX_FeeSchedules_Owner (CustomerId, OwnerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS ManagementFeeTransactions (
  FeeTransactionId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId       BIGINT NOT NULL,
  OwnerId          BIGINT NOT NULL,
  PropertyId       BIGINT NOT NULL,
  PeriodStart      DATE NOT NULL,
  PeriodEnd        DATE NOT NULL,
  GrossRent        DECIMAL(10,2) NOT NULL,
  FeeRate          DECIMAL(8,4) NOT NULL,
  FeeAmount        DECIMAL(10,2) NOT NULL,
  StatusCode       VARCHAR(50) NOT NULL DEFAULT 'PENDING',
  InvoicedDate     DATE NULL,
  PaidDate         DATE NULL,
  CreatedAtUtc     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (OwnerId)    REFERENCES Owners(OwnerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  INDEX IX_FeeTransactions_Owner  (CustomerId, OwnerId, PeriodEnd),
  INDEX IX_FeeTransactions_Status (CustomerId, StatusCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- SEED: Arrears Escalations
-- TenancyId 11 (4 months, $3000 arrears) → full escalation trail
-- TenancyId 12 (4 weeks, $1640 arrears) → reminder + breach notice
-- TenancyId 14 (2 months, $1360 arrears) → two reminders
-- TenancyId  1 (3 weeks, $1842 arrears)  → first reminder
-- TenancyId  2 (2 fortnights, $960)      → first reminder
-- TenancyId  4 (2 months, $1840)         → reminder + breach notice
-- ============================================================
INSERT INTO ArrearsEscalations (CustomerId, TenancyId, Stage, EscalationDate, ArrearsAmountAtStage, Notes, HandledByUserId) VALUES
-- TenancyId 11 — most serious (4 months arrears, $3000)
(1, 11, 'REMINDER_1',    '2026-01-08',  750.00, '14-day arrears letter sent via registered post.',       'manager1'),
(1, 11, 'REMINDER_2',    '2026-01-22', 1500.00, 'Second reminder issued. Tenant promises to pay by end of month.', 'manager1'),
(1, 11, 'BREACH_NOTICE', '2026-02-05', 2250.00, 'Form 11 Breach Notice issued for rent arrears.',       'manager2'),
(1, 11, 'TRIBUNAL',      '2026-02-20', 3000.00, 'VCAT application lodged. Hearing scheduled for March.', 'manager2'),
-- TenancyId 12 — breach notice stage ($1640)
(1, 12, 'REMINDER_1',    '2026-01-26', 1230.00, 'First reminder letter issued. No response from tenant.', 'manager1'),
(1, 12, 'BREACH_NOTICE', '2026-02-09', 1640.00, 'Breach notice issued. Tenant has 14 days to remedy.', 'manager2'),
-- TenancyId 14 — two reminders ($1360)
(1, 14, 'REMINDER_1',    '2026-01-15',  680.00, 'First reminder sent. Tenant acknowledged, agreed to pay.', 'manager1'),
(1, 14, 'REMINDER_2',    '2026-02-10', 1360.00, 'Payment not received. Second reminder issued.',       'manager1'),
-- TenancyId 1 — first reminder ($1842 net after partial payment)
(1,  1, 'REMINDER_1',    '2026-02-04',  614.00, 'First reminder letter sent. Tenant part-paid $300 on 15 Feb.', 'manager1'),
-- TenancyId 2 — first reminder ($960)
(1,  2, 'REMINDER_1',    '2026-01-27',  480.00, 'First reminder issued for fortnightly rent arrears.', 'manager1'),
-- TenancyId 4 — breach notice ($1840)
(1,  4, 'REMINDER_1',    '2026-01-08',  920.00, 'First reminder sent. Tenant on payment plan arrangement.', 'manager2'),
(1,  4, 'BREACH_NOTICE', '2026-02-12', 1840.00, 'Payment plan broken. Breach notice issued.',          'manager2');

-- ============================================================
-- SEED: Management Fee Schedules — one per owner
-- ============================================================
INSERT INTO ManagementFeeSchedules (CustomerId, OwnerId, PropertyId, FeeType, FeeValue, EffectiveFrom, Notes) VALUES
(1,  1, NULL, 'PERCENTAGE', 8.0000, '2023-01-01', 'Standard 8% management fee'),
(1,  2, NULL, 'PERCENTAGE', 8.0000, '2023-01-01', 'Standard 8% management fee'),
(1,  3, NULL, 'PERCENTAGE', 8.5000, '2023-06-01', 'Premium service tier — 8.5%'),
(1,  4, NULL, 'PERCENTAGE', 8.0000, '2023-01-01', 'Standard 8% management fee'),
(1,  5, NULL, 'PERCENTAGE', 7.5000, '2024-01-01', 'Negotiated rate for multi-property owner'),
(1,  6, NULL, 'PERCENTAGE', 8.0000, '2023-01-01', 'Standard 8% management fee'),
(1,  7, NULL, 'FLAT',       55.0000,'2023-09-01', 'Flat monthly fee agreement'),
(1,  8, NULL, 'PERCENTAGE', 8.5000, '2023-01-01', 'Premium service tier — 8.5%'),
(1,  9, NULL, 'PERCENTAGE', 8.0000, '2023-01-01', 'Standard 8% management fee'),
(1, 10, NULL, 'PERCENTAGE', 8.0000, '2022-07-01', 'Standard 8% management fee — long-term client');

-- ============================================================
-- SEED: Management Fee Transactions — Q4 2025 + Q1 2026
-- ============================================================
INSERT INTO ManagementFeeTransactions (CustomerId, OwnerId, PropertyId, PeriodStart, PeriodEnd, GrossRent, FeeRate, FeeAmount, StatusCode, InvoicedDate, PaidDate) VALUES
-- Q1 2026
(1,  1, 1,  '2026-01-01', '2026-03-31', 4800.00, 8.0000,  384.00, 'PAID',    '2026-04-01', '2026-04-10'),
(1,  2, 2,  '2026-01-01', '2026-03-31', 3600.00, 8.0000,  288.00, 'PAID',    '2026-04-01', '2026-04-12'),
(1,  3, 3,  '2026-01-01', '2026-03-31', 5200.00, 8.5000,  442.00, 'INVOICED','2026-04-01', NULL),
(1,  4, 4,  '2026-01-01', '2026-03-31', 7800.00, 8.0000,  624.00, 'INVOICED','2026-04-01', NULL),
(1,  5, 5,  '2026-01-01', '2026-03-31', 3200.00, 7.5000,  240.00, 'PENDING', NULL,         NULL),
(1,  6, 6,  '2026-01-01', '2026-03-31', 6400.00, 8.0000,  512.00, 'PENDING', NULL,         NULL),
(1,  7, 7,  '2026-01-01', '2026-03-31',    0.00, 0.0000,   55.00, 'PENDING', NULL,         NULL),
(1,  8, 8,  '2026-01-01', '2026-03-31', 5600.00, 8.5000,  476.00, 'INVOICED','2026-04-02', NULL),
(1,  9, 9,  '2026-01-01', '2026-03-31', 3800.00, 8.0000,  304.00, 'PENDING', NULL,         NULL),
(1, 10, 10, '2026-01-01', '2026-03-31', 4400.00, 8.0000,  352.00, 'PENDING', NULL,         NULL),
-- Q4 2025
(1,  1, 1,  '2025-10-01', '2025-12-31', 4800.00, 8.0000,  384.00, 'PAID',    '2026-01-05', '2026-01-14'),
(1,  2, 2,  '2025-10-01', '2025-12-31', 3600.00, 8.0000,  288.00, 'PAID',    '2026-01-05', '2026-01-16'),
(1,  3, 3,  '2025-10-01', '2025-12-31', 5200.00, 8.5000,  442.00, 'PAID',    '2026-01-05', '2026-01-18'),
(1,  4, 4,  '2025-10-01', '2025-12-31', 7800.00, 8.0000,  624.00, 'PAID',    '2026-01-05', '2026-01-20'),
(1,  5, 5,  '2025-10-01', '2025-12-31', 3200.00, 7.5000,  240.00, 'PAID',    '2026-01-05', '2026-01-22');

-- ============================================================
-- SEED: Trust Ledger — Q4 2025 + Q1 2026 for owners 1-5
-- Running balance computed per owner
-- ============================================================
INSERT INTO TrustLedger (CustomerId, OwnerId, TransactionDate, TransactionType, Amount, RunningBalance, Reference, Description) VALUES
-- Owner 1
(1, 1, '2025-10-07', 'RENTAL_RECEIPT',  1600.00,  1600.00, 'RLP-10-001', 'Oct rent received — 14 Rosebay Ave'),
(1, 1, '2025-10-15', 'EXPENSE_PAYMENT', -320.00,  1280.00, 'EXP-10-011', 'Plumbing repair payment to ClearDrain'),
(1, 1, '2025-11-07', 'RENTAL_RECEIPT',  1600.00,  2880.00, 'RLP-11-001', 'Nov rent received — 14 Rosebay Ave'),
(1, 1, '2025-11-28', 'FEE_DEDUCTION',   -128.00,  2752.00, 'FEE-11-001', 'Management fee 8% — Nov 2025'),
(1, 1, '2025-11-30', 'DISBURSEMENT',   -2600.00,   152.00, 'DIS-11-001', 'Owner disbursement — Nov 2025'),
(1, 1, '2025-12-07', 'RENTAL_RECEIPT',  1600.00,  1752.00, 'RLP-12-001', 'Dec rent received — 14 Rosebay Ave'),
(1, 1, '2025-12-31', 'FEE_DEDUCTION',   -128.00,  1624.00, 'FEE-12-001', 'Management fee 8% — Dec 2025'),
(1, 1, '2026-01-07', 'RENTAL_RECEIPT',  1600.00,  3224.00, 'RLP-01-001', 'Jan rent received — 14 Rosebay Ave'),
(1, 1, '2026-01-31', 'DISBURSEMENT',   -3000.00,   224.00, 'DIS-01-001', 'Owner disbursement — Jan 2026'),
(1, 1, '2026-02-07', 'RENTAL_RECEIPT',  1600.00,  1824.00, 'RLP-02-001', 'Feb rent received — 14 Rosebay Ave'),
-- Owner 2
(1, 2, '2025-10-14', 'RENTAL_RECEIPT',  1200.00,  1200.00, 'RLP-10-002', 'Oct rent received — 3 Lorne St'),
(1, 2, '2025-11-14', 'RENTAL_RECEIPT',  1200.00,  2400.00, 'RLP-11-002', 'Nov rent received — 3 Lorne St'),
(1, 2, '2025-11-28', 'FEE_DEDUCTION',    -96.00,  2304.00, 'FEE-11-002', 'Management fee 8% — Nov 2025'),
(1, 2, '2025-11-30', 'DISBURSEMENT',   -2200.00,   104.00, 'DIS-11-002', 'Owner disbursement — Nov 2025'),
(1, 2, '2025-12-14', 'RENTAL_RECEIPT',  1200.00,  1304.00, 'RLP-12-002', 'Dec rent received — 3 Lorne St'),
(1, 2, '2026-01-14', 'RENTAL_RECEIPT',  1200.00,  2504.00, 'RLP-01-002', 'Jan rent received — 3 Lorne St'),
(1, 2, '2026-01-31', 'DISBURSEMENT',   -2400.00,   104.00, 'DIS-01-002', 'Owner disbursement — Jan 2026'),
(1, 2, '2026-02-14', 'RENTAL_RECEIPT',  1200.00,  1304.00, 'RLP-02-002', 'Feb rent received — 3 Lorne St'),
-- Owner 3 (8.5% fee)
(1, 3, '2025-10-01', 'RENTAL_RECEIPT',  1733.00,  1733.00, 'RLP-10-003', 'Oct rent received — 22 Collins Pl'),
(1, 3, '2025-10-15', 'EXPENSE_PAYMENT', -450.00,  1283.00, 'EXP-10-031', 'Plumbing works — bathroom'),
(1, 3, '2025-11-01', 'RENTAL_RECEIPT',  1733.00,  3016.00, 'RLP-11-003', 'Nov rent received — 22 Collins Pl'),
(1, 3, '2025-11-30', 'FEE_DEDUCTION',   -147.00,  2869.00, 'FEE-11-003', 'Management fee 8.5% — Nov 2025'),
(1, 3, '2025-11-30', 'DISBURSEMENT',   -2700.00,   169.00, 'DIS-11-003', 'Owner disbursement — Nov 2025'),
(1, 3, '2025-12-01', 'RENTAL_RECEIPT',  1733.00,  1902.00, 'RLP-12-003', 'Dec rent received — 22 Collins Pl'),
(1, 3, '2026-01-01', 'RENTAL_RECEIPT',  1733.00,  3635.00, 'RLP-01-003', 'Jan rent received — 22 Collins Pl'),
(1, 3, '2026-01-31', 'DISBURSEMENT',   -3400.00,   235.00, 'DIS-01-003', 'Owner disbursement — Jan 2026'),
(1, 3, '2026-02-01', 'RENTAL_RECEIPT',  1733.00,  1968.00, 'RLP-02-003', 'Feb rent received — 22 Collins Pl');
