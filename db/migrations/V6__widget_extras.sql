-- V6: Widget extras — auto-refresh, KPI thresholds, NL alert conditions

ALTER TABLE DashboardWidgets
  ADD COLUMN RefreshIntervalMinutes INT          NULL DEFAULT NULL COMMENT 'Auto-refresh interval; NULL = disabled',
  ADD COLUMN ThresholdMin           DECIMAL(18,4) NULL DEFAULT NULL COMMENT 'KPI alert lower bound; NULL = no lower bound',
  ADD COLUMN ThresholdMax           DECIMAL(18,4) NULL DEFAULT NULL COMMENT 'KPI alert upper bound; NULL = no upper bound';

ALTER TABLE ScheduledReports
  ADD COLUMN AlertCondition TEXT NULL DEFAULT NULL COMMENT 'Natural-language alert condition, e.g. "arrears > 5"';
