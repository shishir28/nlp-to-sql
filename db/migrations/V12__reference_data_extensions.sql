-- V12: Reference Data Extensions for new PM domains
-- Adds RefStatusCodes for Vacancy, Application, ArrearsStage, TrustTransaction,
-- ComplianceType, LeaseOutcome, PMTask, Invoice, Listing

INSERT INTO RefStatusCodes (Domain, Code, Label, SortOrder) VALUES
-- Vacancy status
('Vacancy', 'AVAILABLE',    'Available',         1),
('Vacancy', 'LEASED',       'Leased',             2),
('Vacancy', 'MAINTENANCE',  'Under Maintenance',  3),
('Vacancy', 'OFF_MARKET',   'Off Market',         4),

-- Letting Application status
('Application', 'RECEIVED',    'Received',        1),
('Application', 'REVIEWING',   'Under Review',    2),
('Application', 'APPROVED',    'Approved',        3),
('Application', 'DECLINED',    'Declined',        4),
('Application', 'WITHDRAWN',   'Withdrawn',       5),
('Application', 'LEASED',      'Leased',          6),

-- Listing platform / status
('Listing', 'ACTIVE',     'Active',      1),
('Listing', 'PAUSED',     'Paused',      2),
('Listing', 'EXPIRED',    'Expired',     3),
('Listing', 'LEASED',     'Leased',      4),

-- Arrears escalation stage
('ArrearsStage', 'NOTICE_7',     '7-Day Notice',         1),
('ArrearsStage', 'NOTICE_14',    '14-Day Notice',        2),
('ArrearsStage', 'NOTICE_FINAL', 'Final Notice',         3),
('ArrearsStage', 'TRIBUNAL',     'Tribunal Filed',       4),
('ArrearsStage', 'PAYMENT_PLAN', 'Payment Plan Active',  5),
('ArrearsStage', 'RESOLVED',     'Resolved',             6),
('ArrearsStage', 'WRITTEN_OFF',  'Written Off',          7),

-- Trust ledger transaction types
('TrustTransaction', 'RENT_IN',       'Rent Received',          1),
('TrustTransaction', 'MGMT_FEE',      'Management Fee',         2),
('TrustTransaction', 'DISBURSEMENT',  'Owner Disbursement',     3),
('TrustTransaction', 'MAINTENANCE',   'Maintenance Payment',    4),
('TrustTransaction', 'ADVERTISING',   'Advertising Charge',     5),
('TrustTransaction', 'LETTING_FEE',   'Letting Fee',            6),
('TrustTransaction', 'BOND_IN',       'Bond Received',          7),
('TrustTransaction', 'BOND_OUT',      'Bond Disbursed',         8),
('TrustTransaction', 'ADJUSTMENT',    'Manual Adjustment',      9),

-- Compliance item types
('ComplianceType', 'SMOKE_ALARM',    'Smoke Alarm',             1),
('ComplianceType', 'RCD',            'Residual Current Device',  2),
('ComplianceType', 'GAS_CERT',       'Gas Safety Certificate',  3),
('ComplianceType', 'ELEC_CERT',      'Electrical Certificate',  4),
('ComplianceType', 'POOL_SAFETY',    'Pool Safety',             5),
('ComplianceType', 'EPC',            'Energy Performance Cert', 6),
('ComplianceType', 'PEST_INSPECT',   'Pest Inspection',         7),
('ComplianceType', 'BUILDING_CERT',  'Building Certificate',    8),

-- Compliance check result
('ComplianceCheck', 'PASSED',    'Passed',              1),
('ComplianceCheck', 'FAILED',    'Failed',              2),
('ComplianceCheck', 'SCHEDULED', 'Inspection Scheduled',3),
('ComplianceCheck', 'OVERDUE',   'Overdue',             4),
('ComplianceCheck', 'EXEMPT',    'Exempt',              5),

-- Lease renewal outcomes
('LeaseOutcome', 'OFFERED',   'Renewal Offered',        1),
('LeaseOutcome', 'ACCEPTED',  'Renewal Accepted',       2),
('LeaseOutcome', 'DECLINED',  'Tenant Declined',        3),
('LeaseOutcome', 'VACATING',  'Tenant Vacating',        4),
('LeaseOutcome', 'PERIODIC',  'Going Periodic',         5),
('LeaseOutcome', 'EXPIRED',   'Lease Expired',          6),

-- PM Task status
('PMTask', 'OPEN',        'Open',         1),
('PMTask', 'IN_PROGRESS', 'In Progress',  2),
('PMTask', 'DONE',        'Done',         3),
('PMTask', 'CANCELLED',   'Cancelled',    4),
('PMTask', 'BLOCKED',     'Blocked',      5),

-- PM Task category
('PMTaskCategory', 'RENEWAL',      'Lease Renewal',     1),
('PMTaskCategory', 'INSPECTION',   'Inspection',        2),
('PMTaskCategory', 'MAINTENANCE',  'Maintenance',       3),
('PMTaskCategory', 'ARREARS',      'Arrears Follow-up', 4),
('PMTaskCategory', 'COMPLIANCE',   'Compliance',        5),
('PMTaskCategory', 'LETTING',      'Letting',           6),
('PMTaskCategory', 'ADMIN',        'Administration',    7),
('PMTaskCategory', 'OTHER',        'Other',             8),

-- Invoice payment status
('Invoice', 'DRAFT',    'Draft',       1),
('Invoice', 'SENT',     'Sent',        2),
('Invoice', 'PAID',     'Paid',        3),
('Invoice', 'OVERDUE',  'Overdue',     4),
('Invoice', 'VOID',     'Void',        5),

-- Maintenance quote status
('Quote', 'REQUESTED',   'Requested',          1),
('Quote', 'RECEIVED',    'Received',           2),
('Quote', 'APPROVED',    'Owner Approved',     3),
('Quote', 'DECLINED',    'Owner Declined',     4),
('Quote', 'ENGAGED',     'Contractor Engaged', 5)

ON DUPLICATE KEY UPDATE Label = VALUES(Label), SortOrder = VALUES(SortOrder);
