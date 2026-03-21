export interface ColumnInfo {
  name: string;
  type: string;
}

export interface QueryResponse {
  requestId: string;
  status: string;
  rows: Record<string, any>[];
  columns: ColumnInfo[];
  rowCount: number;
  executionMs?: number;
  domain?: string;
  explanation?: string;
  message?: string;
  errorCode?: string;
  nlSummary?: string;
  generatedSql?: string;
}

export interface NlQueryRequest {
  question: string;
  conversationId?: string;
  customerId?: string;
  role?: string;
}

// ─── Property Management Domain Models ──────────────────────────────────────

export interface VacancyDto {
  vacancyId: number;
  propertyId: number;
  propertyAddress: string;
  status: string;
  advertisedRent: number | null;
  rentFrequency: string | null;
  availableFrom: string | null;
  daysVacant: number;
  enquiryCount: number;
  applicationCount: number;
  createdAtUtc: string;
}

export interface LettingApplicationDto {
  applicationId: number;
  vacancyId: number;
  propertyAddress: string;
  applicantName: string;
  applicantEmail: string | null;
  applicantPhone: string | null;
  status: string;
  proposedMoveIn: string | null;
  offeredRent: number | null;
  applicantCount: number | null;
  notes: string | null;
  submittedAt: string;
}

export interface ArrearsEscalationDto {
  escalationId: number;
  tenancyId: number;
  propertyId: number;
  propertyAddress: string;
  tenantName: string;
  escalationStage: string;
  arrearsAmount: number;
  arrearsDays: number;
  escalationDate: string;
  nextActionDate: string | null;
  notes: string | null;
  handledByUserId: string | null;
  isResolved: boolean;
}

export interface ArrearsSummaryDto {
  totalTenanciesInArrears: number;
  totalArrearsAmount: number;
  atTribunalCount: number;
  onPaymentPlanCount: number;
  escalations: ArrearsEscalationDto[];
}

export interface ComplianceItemDto {
  complianceItemId: number;
  propertyId: number;
  propertyAddress: string;
  complianceType: string;
  description: string;
  dueDate: string | null;
  lastCheckedDate: string | null;
  status: string;
  isOverdue: boolean;
  daysUntilDue: number;
  notes: string | null;
}

export interface ComplianceSummaryDto {
  totalItems: number;
  overdueCount: number;
  dueSoonCount: number;
  passedCount: number;
  overdueItems: ComplianceItemDto[];
  dueSoonItems: ComplianceItemDto[];
}

export interface TrustLedgerEntryDto {
  trustLedgerId: number;
  ownerId: number;
  ownerName: string;
  propertyId: number | null;
  propertyAddress: string | null;
  transactionType: string;
  amount: number;
  runningBalance: number;
  transactionDate: string;
  reference: string | null;
  description: string | null;
  createdAtUtc: string;
}

export interface TrustLedgerSummaryDto {
  ownerId: number;
  ownerName: string;
  currentBalance: number;
  totalRentIn: number;
  totalDisbursed: number;
  totalFees: number;
  recentEntries: TrustLedgerEntryDto[];
}

export interface PMTaskDto {
  taskId: number;
  title: string;
  description: string | null;
  category: string;
  priority: string;
  status: string;
  assignedToUserId: string;
  dueDate: string | null;
  isOverdue: boolean;
  propertyId: number | null;
  propertyAddress: string | null;
  tenancyId: number | null;
  maintenanceJobId: number | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
}

export interface PMTaskSummaryDto {
  openCount: number;
  overdueCount: number;
  dueTodayCount: number;
  dueThisWeekCount: number;
  tasks: PMTaskDto[];
}

export interface LeaseRenewalOutcomeDto {
  outcomeId: number;
  tenancyId: number;
  propertyId: number;
  propertyAddress: string;
  tenantName: string;
  leaseEndDate: string | null;
  proposedNewRent: number | null;
  proposedStartDate: string | null;
  outcomeCode: string;
  outcomeDate: string;
  notes: string | null;
  handledByUserId: string | null;
}

export interface LeaseRenewalSummaryDto {
  totalUpcoming: number;
  offeredCount: number;
  acceptedCount: number;
  vacatingCount: number;
  periodicCount: number;
  renewals: LeaseRenewalOutcomeDto[];
}
