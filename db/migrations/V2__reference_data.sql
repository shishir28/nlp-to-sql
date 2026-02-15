-- Insert Australian states reference data
INSERT INTO RefStatesAU (StateCode, StateName) VALUES
('NSW', 'New South Wales'),
('VIC', 'Victoria'),
('QLD', 'Queensland'),
('WA', 'Western Australia'),
('SA', 'South Australia'),
('TAS', 'Tasmania'),
('ACT', 'Australian Capital Territory'),
('NT', 'Northern Territory')
ON DUPLICATE KEY UPDATE StateName = VALUES(StateName);

-- Insert status codes for various domains
INSERT INTO RefStatusCodes (Domain, Code, Label, SortOrder) VALUES
('Tenancy', 'ACTIVE', 'Active', 1),
('Tenancy', 'ENDED', 'Ended', 2),
('Tenancy', 'PENDING', 'Pending', 3),
('Maintenance', 'OPEN', 'Open', 1),
('Maintenance', 'IN_PROGRESS', 'In Progress', 2),
('Maintenance', 'AWAITING_PARTS', 'Awaiting Parts', 3),
('Maintenance', 'CLOSED', 'Closed', 4),
('Maintenance', 'CANCELLED', 'Cancelled', 5),
('Inspection', 'SCHEDULED', 'Scheduled', 1),
('Inspection', 'COMPLETED', 'Completed', 2),
('Inspection', 'OVERDUE', 'Overdue', 3),
('Inspection', 'CANCELLED', 'Cancelled', 4),
('Compliance', 'PASSED', 'Passed', 1),
('Compliance', 'FAILED', 'Failed', 2),
('Compliance', 'PENDING', 'Pending', 3),
('Priority', 'LOW', 'Low', 1),
('Priority', 'MEDIUM', 'Medium', 2),
('Priority', 'HIGH', 'High', 3),
('Priority', 'URGENT', 'Urgent', 4)
ON DUPLICATE KEY UPDATE Label = VALUES(Label), SortOrder = VALUES(SortOrder);
