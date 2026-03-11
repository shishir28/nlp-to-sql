t-- Widget layout persistence: stores each customer's live dashboard widgets
CREATE TABLE IF NOT EXISTS DashboardWidgets (
    Id           INT          AUTO_INCREMENT PRIMARY KEY,
    CustomerId   VARCHAR(50)  NOT NULL,
    UserId       VARCHAR(100) NOT NULL,
    Title        VARCHAR(200) NOT NULL,
    Question     TEXT         NOT NULL,
    ViewType     VARCHAR(10)  NOT NULL DEFAULT 'table',
    SortOrder    INT          NOT NULL DEFAULT 0,
    CreatedAtUtc DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_dw_customer (CustomerId, SortOrder)
);
