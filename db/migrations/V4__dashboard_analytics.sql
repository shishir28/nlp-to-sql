-- Dashboard: Saved / Pinned Queries
CREATE TABLE IF NOT EXISTS SavedQueries (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    CustomerId   VARCHAR(50)  NOT NULL,
    UserId       VARCHAR(100) NOT NULL,
    Name         VARCHAR(200) NOT NULL,
    Question     TEXT         NOT NULL,
    Role         VARCHAR(50)  NOT NULL DEFAULT 'PropertyManager',
    IsPinned     TINYINT(1)   NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_sq_customer (CustomerId),
    INDEX idx_sq_user (UserId)
);

-- Analytics: persisted query audit log
CREATE TABLE IF NOT EXISTS QueryAuditLog (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    RequestId    VARCHAR(50)  NOT NULL,
    CustomerId   VARCHAR(50),
    UserId       VARCHAR(100),
    Role         VARCHAR(50),
    Question     TEXT,
    Domain       VARCHAR(50),
    Status       VARCHAR(30),
    ExecutionMs  BIGINT       DEFAULT 0,
    RowCount     INT          DEFAULT 0,
    ErrorCode    VARCHAR(50),
    CreatedAtUtc DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_qal_customer  (CustomerId),
    INDEX idx_qal_status    (Status),
    INDEX idx_qal_domain    (Domain),
    INDEX idx_qal_created   (CreatedAtUtc)
);

-- Scheduled Reports
CREATE TABLE IF NOT EXISTS ScheduledReports (
    Id             INT AUTO_INCREMENT PRIMARY KEY,
    CustomerId     VARCHAR(50)  NOT NULL,
    UserId         VARCHAR(100) NOT NULL,
    Name           VARCHAR(200) NOT NULL,
    Question       TEXT         NOT NULL,
    Role           VARCHAR(50)  NOT NULL DEFAULT 'PropertyManager',
    RecipientEmail VARCHAR(200) NOT NULL,
    Schedule       VARCHAR(20)  NOT NULL DEFAULT 'daily',  -- daily | weekly | monthly
    IsActive       TINYINT(1)   NOT NULL DEFAULT 1,
    LastRunAtUtc   DATETIME,
    NextRunAtUtc   DATETIME,
    CreatedAtUtc   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_sr_customer (CustomerId),
    INDEX idx_sr_next_run (NextRunAtUtc, IsActive)
);
