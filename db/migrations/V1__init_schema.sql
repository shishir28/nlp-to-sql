-- Multi-tenant Australian Property Management Schema
-- All tenant-owned tables include CustomerId for row-level security

CREATE TABLE IF NOT EXISTS Customers (
  CustomerId BIGINT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(200) NOT NULL,
  Abn VARCHAR(20) NULL COMMENT 'Australian Business Number',
  Email VARCHAR(200) NULL,
  IsActive BIT NOT NULL DEFAULT b'1',
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX IX_Customers_IsActive (IsActive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Global reference tables (no CustomerId)
CREATE TABLE IF NOT EXISTS RefStatesAU (
  StateCode CHAR(3) PRIMARY KEY,
  StateName VARCHAR(64) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS RefStatusCodes (
  Domain VARCHAR(50) NOT NULL,
  Code VARCHAR(50) NOT NULL,
  Label VARCHAR(100) NOT NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  PRIMARY KEY (Domain, Code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Owners (
  OwnerId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  FullName VARCHAR(200) NOT NULL,
  Email VARCHAR(200) NULL,
  Phone VARCHAR(32) NULL,
  Abn VARCHAR(20) NULL,
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  INDEX IX_Owners_CustomerId (CustomerId, OwnerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Properties (
  PropertyId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  AddressLine1 VARCHAR(255) NOT NULL,
  AddressLine2 VARCHAR(255) NULL,
  Suburb VARCHAR(128) NOT NULL,
  StateCode CHAR(3) NOT NULL,
  Postcode VARCHAR(8) NOT NULL,
  PropertyType VARCHAR(50) NOT NULL COMMENT 'House, Unit, Townhouse, etc',
  Bedrooms TINYINT NULL,
  Bathrooms TINYINT NULL,
  Parking TINYINT NULL,
  IsActive BIT NOT NULL DEFAULT b'1',
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (StateCode) REFERENCES RefStatesAU(StateCode),
  INDEX IX_Properties_CustomerId (CustomerId, PropertyId),
  INDEX IX_Properties_State (CustomerId, StateCode, Suburb)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS PropertyOwners (
  PropertyOwnerId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  PropertyId BIGINT NOT NULL,
  OwnerId BIGINT NOT NULL,
  OwnershipPct DECIMAL(5,2) NOT NULL DEFAULT 100.00,
  StartDate DATE NOT NULL,
  EndDate DATE NULL,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  FOREIGN KEY (OwnerId) REFERENCES Owners(OwnerId),
  INDEX IX_PropertyOwners_Customer (CustomerId, PropertyId, OwnerId),
  INDEX IX_PropertyOwners_Owner (CustomerId, OwnerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Tenants (
  TenantId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  FullName VARCHAR(200) NOT NULL,
  Email VARCHAR(200) NULL,
  Phone VARCHAR(32) NULL,
  EmergencyContact VARCHAR(255) NULL,
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  INDEX IX_Tenants_CustomerId (CustomerId, TenantId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Tenancies (
  TenancyId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  PropertyId BIGINT NOT NULL,
  TenantId BIGINT NOT NULL,
  StartDate DATE NOT NULL,
  EndDate DATE NULL,
  LeaseEndDate DATE NULL COMMENT 'Fixed-term lease expiry',
  RentAmount DECIMAL(10,2) NOT NULL,
  RentFrequency VARCHAR(20) NOT NULL COMMENT 'Weekly, Fortnightly, Monthly',
  BondAmount DECIMAL(10,2) NOT NULL,
  StatusCode VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  FOREIGN KEY (TenantId) REFERENCES Tenants(TenantId),
  INDEX IX_Tenancies_Customer (CustomerId, StatusCode, LeaseEndDate),
  INDEX IX_Tenancies_Property (CustomerId, PropertyId),
  INDEX IX_Tenancies_Tenant (CustomerId, TenantId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS RentLedgerEntries (
  EntryId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  TenancyId BIGINT NOT NULL,
  EntryType VARCHAR(30) NOT NULL COMMENT 'Charge, Receipt, Adjustment',
  TransactionDate DATE NOT NULL,
  DueDate DATE NOT NULL,
  PaidDate DATE NULL,
  Amount DECIMAL(10,2) NOT NULL,
  BalanceDelta DECIMAL(10,2) NOT NULL COMMENT 'Impact on arrears (+/-)',
  Description VARCHAR(255) NULL,
  Reference VARCHAR(100) NULL,
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (TenancyId) REFERENCES Tenancies(TenancyId),
  INDEX IX_Ledger_Customer_Tenancy (CustomerId, TenancyId, DueDate),
  INDEX IX_Ledger_Customer_DueDate (CustomerId, DueDate, EntryType),
  INDEX IX_Ledger_Arrears (CustomerId, PaidDate, DueDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Vendors (
  VendorId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  VendorName VARCHAR(200) NOT NULL,
  Category VARCHAR(100) NULL COMMENT 'Plumbing, Electrical, Landscaping, etc',
  Abn VARCHAR(20) NULL,
  Phone VARCHAR(32) NULL,
  Email VARCHAR(200) NULL,
  IsActive BIT NOT NULL DEFAULT b'1',
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  INDEX IX_Vendors_CustomerId (CustomerId, VendorId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS MaintenanceJobs (
  MaintenanceJobId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  PropertyId BIGINT NOT NULL,
  TenancyId BIGINT NULL,
  VendorId BIGINT NULL,
  StatusCode VARCHAR(50) NOT NULL DEFAULT 'OPEN',
  Priority VARCHAR(30) NOT NULL DEFAULT 'MEDIUM',
  Category VARCHAR(100) NULL,
  Summary VARCHAR(255) NOT NULL,
  Description TEXT NULL,
  OpenedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ClosedAtUtc DATETIME NULL,
  EstimatedCost DECIMAL(10,2) NULL,
  ActualCost DECIMAL(10,2) NULL,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  FOREIGN KEY (TenancyId) REFERENCES Tenancies(TenancyId),
  FOREIGN KEY (VendorId) REFERENCES Vendors(VendorId),
  INDEX IX_Maint_Customer_Status (CustomerId, StatusCode, OpenedAtUtc),
  INDEX IX_Maint_Customer_Property (CustomerId, PropertyId),
  INDEX IX_Maint_Customer_Vendor (CustomerId, VendorId),
  INDEX IX_Maint_Age (CustomerId, StatusCode, ClosedAtUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Inspections (
  InspectionId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  PropertyId BIGINT NOT NULL,
  TenancyId BIGINT NULL,
  InspectionType VARCHAR(50) NOT NULL COMMENT 'Routine, Entry, Exit, Compliance',
  ScheduledDate DATE NOT NULL,
  CompletedDate DATE NULL,
  ComplianceStatus VARCHAR(50) NOT NULL DEFAULT 'PENDING',
  Notes TEXT NULL,
  InspectorName VARCHAR(200) NULL,
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  FOREIGN KEY (TenancyId) REFERENCES Tenancies(TenancyId),
  INDEX IX_Inspections_Customer_Schedule (CustomerId, ScheduledDate, ComplianceStatus),
  INDEX IX_Inspections_Customer_Property (CustomerId, PropertyId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS OwnerStatements (
  OwnerStatementId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  OwnerId BIGINT NOT NULL,
  PeriodStart DATE NOT NULL,
  PeriodEnd DATE NOT NULL,
  GrossIncome DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  Expenses DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  ManagementFees DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  NetPayout DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (OwnerId) REFERENCES Owners(OwnerId),
  INDEX IX_OwnerStatements_Customer_Owner (CustomerId, OwnerId, PeriodEnd)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS OwnerStatementLines (
  OwnerStatementLineId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId BIGINT NOT NULL,
  OwnerStatementId BIGINT NOT NULL,
  LineType VARCHAR(50) NOT NULL COMMENT 'Income, Expense, Fee',
  Description VARCHAR(255) NOT NULL,
  Amount DECIMAL(12,2) NOT NULL,
  ReferenceId BIGINT NULL,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (OwnerStatementId) REFERENCES OwnerStatements(OwnerStatementId),
  INDEX IX_OwnerStatementLines_Customer_Statement (CustomerId, OwnerStatementId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
