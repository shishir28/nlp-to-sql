-- V11: Lease Renewal Outcomes + Conversation History

CREATE TABLE IF NOT EXISTS LeaseRenewalOutcomes (
  OutcomeId          BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId         BIGINT NOT NULL,
  TenancyId          BIGINT NOT NULL,
  ProposedNewRent    DECIMAL(10,2) NULL,
  ProposedStartDate  DATE NULL,
  OutcomeCode        VARCHAR(50) NOT NULL,
  OutcomeDate        DATE NOT NULL,
  Notes              TEXT NULL,
  HandledByUserId    VARCHAR(100) NULL,
  CreatedAtUtc       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  FOREIGN KEY (TenancyId)  REFERENCES Tenancies(TenancyId),
  INDEX IX_RenewalOutcomes_Customer  (CustomerId, TenancyId),
  INDEX IX_RenewalOutcomes_Code      (CustomerId, OutcomeCode, OutcomeDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS ConversationHistory (
  ConversationHistoryId BIGINT AUTO_INCREMENT PRIMARY KEY,
  CustomerId            BIGINT NULL,
  ConversationId        VARCHAR(100) NOT NULL,
  TurnNumber            INT NOT NULL DEFAULT 1,
  Question              TEXT NOT NULL,
  GeneratedSql          TEXT NULL,
  Domain                VARCHAR(50) NULL,
  Status                VARCHAR(30) NULL,
  RowCount              INT NULL,
  NlSummary             TEXT NULL,
  CreatedAtUtc          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
  INDEX IX_Conversation_Id    (CustomerId, ConversationId, TurnNumber),
  INDEX IX_Conversation_Date  (CustomerId, CreatedAtUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- SEED: Lease Renewal Outcomes
-- Tenancies with upcoming lease ends set in V3 (TenancyIds 11-17)
-- Plus some earlier renewals for history
-- ============================================================
INSERT INTO LeaseRenewalOutcomes (CustomerId, TenancyId, ProposedNewRent, ProposedStartDate, OutcomeCode, OutcomeDate, Notes, HandledByUserId) VALUES
-- TenancyId 13 (lease ends Apr 2026) — accepted renewal
(1, 13, 640.00, '2026-05-01', 'ACCEPTED', '2026-02-20', 'Tenant accepted 3% rent increase from $620 to $640/wk. New lease signed.', 'manager2'),
-- TenancyId 14 (lease ends May 2026) — offered, awaiting response
(1, 14, 700.00, '2026-06-01', 'OFFERED',  '2026-02-28', 'Renewal letter sent. Proposed $700/mo increase. Awaiting tenant response.', 'manager1'),
-- TenancyId 15 (lease ends Jun 2026) — tenant vacating
(1, 15, NULL,   NULL,         'VACATING', '2026-02-15', 'Tenant advised not renewing. Relocating to Brisbane. Vacate date confirmed June 30.', 'manager1'),
-- TenancyId 16 (lease ends Jul 2026) — offered, no response yet
(1, 16, 640.00, '2026-08-01', 'OFFERED',  '2026-03-01', 'Renewal offer sent 3 months prior. No response received yet.', 'manager1'),
-- TenancyId 17 (lease ends Aug 2026) — going periodic
(1, 17, NULL,   NULL,         'PERIODIC', '2026-03-05', 'Tenant requested periodic tenancy — not ready to commit to fixed term.', 'manager2'),
-- TenancyId 11 (lease ends Sep 2026) — at tribunal, renewal on hold
(1, 11, NULL,   NULL,         'OFFERED',  '2026-01-15', 'Renewal offered but tenant has significant arrears. On hold pending VCAT outcome.', 'manager2'),
-- TenancyId 12 (lease ends Oct 2026) — declined, breach notice active
(1, 12, 430.00, '2026-11-01', 'DECLINED', '2026-02-05', 'Tenant declined renewal. Intends to vacate at lease end if arrears resolved.', 'manager1'),
-- Historical renewals for tenancies 1-5
(1,  1, 620.00, '2025-07-01', 'ACCEPTED', '2025-04-10', 'Tenant accepted. Rent increased from $600 to $614/wk.', 'manager1'),
(1,  2, 490.00, '2025-05-01', 'ACCEPTED', '2025-02-15', 'Tenant happy to renew. Fortnightly $480 retained.', 'manager2'),
(1,  3, 540.00, '2025-09-01', 'ACCEPTED', '2025-06-08', 'Renewed. Minor increase $530→$540.', 'manager1'),
(1,  4, 940.00, '2025-08-01', 'ACCEPTED', '2025-05-20', 'Tenant accepted. Monthly rent up from $920 to $940.', 'manager2'),
(1,  5, 330.00, '2025-10-01', 'VACATING', '2025-07-01', 'Tenant vacated at lease end. New tenancy arranged.', 'manager1');

-- ============================================================
-- SEED: Conversation History — 10 realistic PM conversations
-- ============================================================
INSERT INTO ConversationHistory (CustomerId, ConversationId, TurnNumber, Question, Domain, Status, RowCount, NlSummary) VALUES
(1, 'conv-001', 1, 'Which tenancies have arrears?',                             'arrears',    'ok', 8,  'Found 8 tenancies with outstanding rent totalling $13,248.'),
(1, 'conv-001', 2, 'Show me only those over $1000 arrears',                    'arrears',    'ok', 5,  'Found 5 tenancies with arrears over $1,000.'),
(1, 'conv-001', 3, 'What is the escalation stage for each of these tenancies?', 'arrears_escalation','ok',5,'All 5 have escalation records. TenancyId 11 is at TRIBUNAL stage.'),
(1, 'conv-002', 1, 'Show properties currently vacant',                         'vacancy',    'ok', 5,  '5 properties are currently available, longest vacant 79 days.'),
(1, 'conv-002', 2, 'Which of these have active listings on Domain?',           'letting',    'ok', 4,  '4 vacant properties have active Domain listings.'),
(1, 'conv-003', 1, 'Show open maintenance jobs',                               'maintenance','ok', 10, '10 open or in-progress maintenance jobs. 2 are URGENT.'),
(1, 'conv-003', 2, 'Which of these are awaiting owner approval on a quote?',   'maintenance_workflow','ok',3,'3 jobs have quotes received but not yet approved.'),
(1, 'conv-004', 1, 'Show my open tasks due this week',                         'pm_tasks',   'ok', 6,  '6 tasks assigned to you are due within the next 7 days.'),
(1, 'conv-005', 1, 'Show compliance items overdue',                            'compliance_calendar','ok',5,'5 compliance items are overdue across 4 properties.'),
(1, 'conv-005', 2, 'Show the overdue ones for Properties 11 and 12',           'compliance_calendar','ok',3,'3 overdue compliance items found for properties 11 and 12.');
