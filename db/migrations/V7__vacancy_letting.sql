-- V7: Vacancy & Letting Pipeline
-- Tables: Vacancies, Listings, LettingEnquiries, LettingApplications

CREATE TABLE IF NOT EXISTS Vacancies (
  VacancyId       BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId      BIGINT NOT NULL,
  PropertyId      BIGINT NOT NULL,
  VacancyDate     DATE NOT NULL,
  TargetRentWeekly DECIMAL(10,2) NULL,
  StatusCode      VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE',
  DaysOnMarket    INT NOT NULL DEFAULT 0,
  Notes           TEXT NULL,
  CreatedAtUtc    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  INDEX IX_Vacancies_Customer_Status (CustomerId, StatusCode, VacancyDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Listings (
  ListingId            BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId           BIGINT NOT NULL,
  PropertyId           BIGINT NOT NULL,
  VacancyId            BIGINT NULL,
  ListingPortal        VARCHAR(100) NOT NULL,
  AdvertisedRentWeekly DECIMAL(10,2) NOT NULL,
  ListedDate           DATE NOT NULL,
  RemovedDate          DATE NULL,
  StatusCode           VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
  ClickCount           INT NOT NULL DEFAULT 0,
  EnquiryCount         INT NOT NULL DEFAULT 0,
  CreatedAtUtc         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (PropertyId) REFERENCES Properties(PropertyId),
  FOREIGN KEY (VacancyId)  REFERENCES Vacancies(VacancyId),
  INDEX IX_Listings_Customer_Status (CustomerId, StatusCode, ListedDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS LettingEnquiries (
  EnquiryId       BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId      BIGINT NOT NULL,
  ListingId       BIGINT NOT NULL,
  ProspectName    VARCHAR(200) NOT NULL,
  ProspectEmail   VARCHAR(200) NULL,
  ProspectPhone   VARCHAR(32) NULL,
  EnquiryDate     DATE NOT NULL,
  StatusCode      VARCHAR(50) NOT NULL DEFAULT 'NEW',
  Notes           TEXT NULL,
  CreatedAtUtc    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (ListingId)  REFERENCES Listings(ListingId),
  INDEX IX_Enquiries_Customer_Listing (CustomerId, ListingId, StatusCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS LettingApplications (
  ApplicationId    BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId       BIGINT NOT NULL,
  ListingId        BIGINT NOT NULL,
  ApplicantName    VARCHAR(200) NOT NULL,
  ApplicantEmail   VARCHAR(200) NULL,
  ApplicantPhone   VARCHAR(32) NULL,
  ApplicationDate  DATE NOT NULL,
  StatusCode       VARCHAR(50) NOT NULL DEFAULT 'RECEIVED',
  EmploymentStatus VARCHAR(100) NULL,
  WeeklyIncome     DECIMAL(10,2) NULL,
  Notes            TEXT NULL,
  CreatedAtUtc     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (ListingId)  REFERENCES Listings(ListingId),
  INDEX IX_Applications_Customer_Listing (CustomerId, ListingId, StatusCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- SEED: Vacancies — properties 7,15,16,17,18 are vacant/available
-- ============================================================
INSERT INTO Vacancies (CustomerId, PropertyId, VacancyDate, TargetRentWeekly, StatusCode, DaysOnMarket, Notes) VALUES
(1,  7, '2026-02-01', 550.00, 'AVAILABLE', 48,  'Previous tenant vacated end of Jan. Property freshly painted and cleaned.'),
(1, 15, '2026-01-15', 480.00, 'AVAILABLE', 65,  'Lease expired. Tenant relocated interstate. Garden needs maintenance.'),
(1, 16, '2026-02-15', 620.00, 'AVAILABLE', 34,  'Tenant gave 30-day notice. Expecting good interest — premium suburb.'),
(1, 17, '2026-01-01', 395.00, 'AVAILABLE', 79,  'Long-term vacancy. Price reduced from $420/wk. Needs fresh marketing.'),
(1, 18, '2026-03-01', 700.00, 'AVAILABLE', 20,  'New acquisition coming to market. Renovated kitchen and bathrooms.'),
(1,  3, '2025-12-01', 530.00, 'LET',       42,  'Successfully let to new tenant starting March 2026.'),
(1,  9, '2025-11-15', 460.00, 'LET',       28,  'Let quickly — great property. New tenancy commencing Dec 2025.');

-- ============================================================
-- SEED: Listings — active listings across major portals
-- ============================================================
INSERT INTO Listings (CustomerId, PropertyId, VacancyId, ListingPortal, AdvertisedRentWeekly, ListedDate, RemovedDate, StatusCode, ClickCount, EnquiryCount) VALUES
(1,  7, 1, 'Domain',   550.00, '2026-02-03', NULL,         'ACTIVE',  342, 14),
(1,  7, 1, 'REA',      550.00, '2026-02-03', NULL,         'ACTIVE',  289, 11),
(1, 15, 2, 'Domain',   480.00, '2026-01-17', NULL,         'ACTIVE',  218,  8),
(1, 15, 2, 'Flatmates',480.00, '2026-01-17', NULL,         'ACTIVE',   95,  5),
(1, 16, 3, 'Domain',   620.00, '2026-02-17', NULL,         'ACTIVE',  401, 18),
(1, 16, 3, 'REA',      620.00, '2026-02-17', NULL,         'ACTIVE',  367, 15),
(1, 17, 4, 'Domain',   395.00, '2026-01-05', NULL,         'ACTIVE',  124,  4),
(1, 18, 5, 'Domain',   700.00, '2026-03-03', NULL,         'ACTIVE',  180,  7),
(1, 18, 5, 'REA',      700.00, '2026-03-03', NULL,         'ACTIVE',  155,  6),
-- Removed/leased listings
(1,  3, 6, 'Domain',   530.00, '2025-12-03', '2026-01-14', 'LEASED',  510, 22),
(1,  9, 7, 'REA',      460.00, '2025-11-17', '2025-12-15', 'LEASED',  430, 19);

-- ============================================================
-- SEED: Enquiries — realistic Australian prospects
-- ============================================================
INSERT INTO LettingEnquiries (CustomerId, ListingId, ProspectName, ProspectEmail, ProspectPhone, EnquiryDate, StatusCode, Notes) VALUES
(1, 1, 'Oliver Hartmann',    'oliver.h@email.com',   '0411 234 567', '2026-02-05', 'APPLICATION',  'Interested. Asked about parking. Applied online.'),
(1, 1, 'Priya Nair',         'priya.nair@gmail.com', '0422 345 678', '2026-02-06', 'FOLLOWUP',     'Wants to inspect Saturday morning.'),
(1, 1, 'Jack Morrison',      'jmorrison@live.com',   '0433 456 789', '2026-02-08', 'NEW',          'Enquired via Domain. No response yet.'),
(1, 2, 'Sophie Chen',        'schen92@gmail.com',    '0444 567 890', '2026-02-04', 'APPLICATION',  'Applied. Young professional. Strong references.'),
(1, 3, 'Ahmed Al-Rashid',    'ahmed.r@outlook.com',  '0455 678 901', '2026-01-19', 'DECLINED',     'Could not provide sufficient rental history.'),
(1, 3, 'Emma Fitzgerald',    'efitz@gmail.com',      '0466 789 012', '2026-01-20', 'APPLICATION',  'Second inspection booked. Very keen.'),
(1, 5, 'Liam OBrien',        'liamob@email.com',     '0477 890 123', '2026-02-18', 'APPLICATION',  'Inspected. Wants to apply. Income verified.'),
(1, 5, 'Isabelle Tremblay',  'is.tremb@gmail.com',   '0488 901 234', '2026-02-19', 'NEW',          'Initial enquiry. Has two cats — checking pet policy.'),
(1, 5, 'Marcus Webb',        'mwebb@hotmail.com',    '0499 012 345', '2026-02-20', 'FOLLOWUP',     'Inspected Saturday. Sent follow-up email.'),
(1, 6, 'Chloe Davidson',     'chloe.d@gmail.com',    '0411 123 456', '2026-02-18', 'NEW',          'Enquired about move-in date.'),
(1, 7, 'Nathan Park',        'npark@email.com',      '0422 234 567', '2026-01-08', 'DECLINED',     'Income insufficient for rent. Declined politely.'),
(1, 8, 'Zoe Williams',       'zoew@gmail.com',       '0433 345 678', '2026-03-05', 'NEW',          'First enquiry received since listing went live.'),
(1, 9, 'Thomas Briggs',      'tbriggs@live.com',     '0444 456 789', '2026-03-06', 'FOLLOWUP',     'Keen on property. Works from home — asked about NBN.');

-- ============================================================
-- SEED: Applications
-- ============================================================
INSERT INTO LettingApplications (CustomerId, ListingId, ApplicantName, ApplicantEmail, ApplicantPhone, ApplicationDate, StatusCode, EmploymentStatus, WeeklyIncome, Notes) VALUES
(1, 1, 'Oliver Hartmann',   'oliver.h@email.com',   '0411 234 567', '2026-02-09', 'PROCESSING', 'Full-time',     1450.00, 'Processing references. 3yr rental history. No pets.'),
(1, 2, 'Sophie Chen',       'schen92@gmail.com',    '0444 567 890', '2026-02-07', 'APPROVED',   'Full-time',     1800.00, 'Excellent references. IT professional. Approved.'),
(1, 3, 'Emma Fitzgerald',   'efitz@gmail.com',      '0466 789 012', '2026-01-23', 'PROCESSING', 'Part-time',     1100.00, 'Has guarantor. Checking documentation.'),
(1, 5, 'Liam OBrien',       'liamob@email.com',     '0477 890 123', '2026-02-21', 'RECEIVED',   'Full-time',     2100.00, 'Application received. Awaiting ID documents.'),
(1, 5, 'Marcus Webb',       'mwebb@hotmail.com',    '0499 012 345', '2026-02-22', 'RECEIVED',   'Self-employed', 1750.00, 'Application received. Requesting 2yr tax returns.'),
(1, 7, 'Nathan Park',       'npark@email.com',      '0422 234 567', '2026-01-10', 'DECLINED',   'Casual',         820.00, 'Income below 3x rent threshold. Application declined.');
