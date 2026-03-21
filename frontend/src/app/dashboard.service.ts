import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import {
  VacancyDto, LettingApplicationDto,
  ArrearsSummaryDto, ArrearsEscalationDto,
  ComplianceSummaryDto, ComplianceItemDto,
  TrustLedgerSummaryDto, TrustLedgerEntryDto,
  PMTaskSummaryDto, PMTaskDto,
  LeaseRenewalSummaryDto, LeaseRenewalOutcomeDto
} from "./models";

export interface SavedQueryDto {
  id: number;
  name: string;
  question: string;
  role: string;
  isPinned: boolean;
  createdAtUtc: string;
}

export interface SaveQueryRequest {
  name: string;
  question: string;
  role: string;
}

export interface ScheduledReportDto {
  id: number;
  name: string;
  question: string;
  role: string;
  recipientEmail: string;
  schedule: string;
  alertCondition: string | null;
  isActive: boolean;
  lastRunAtUtc: string | null;
  nextRunAtUtc: string | null;
  createdAtUtc: string;
}

export interface CreateScheduledReportRequest {
  name: string;
  question: string;
  role: string;
  recipientEmail: string;
  schedule: string;
  alertCondition?: string;
}

export interface DashboardWidgetRecord {
  id: number;
  title: string;
  question: string;
  viewType: string;
  sortOrder: number;
  refreshIntervalMinutes: number | null;
  thresholdMin: number | null;
  thresholdMax: number | null;
  createdAtUtc: string;
}

export interface SaveDashboardWidgetRequest {
  title: string;
  question: string;
  viewType: string;
  sortOrder: number;
}

export interface AnalyticsSummary {
  totalQueries: number;
  successfulQueries: number;
  failedQueries: number;
  avgExecutionMs: number;
  domainBreakdown: { domain: string; count: number }[];
  dailyTrend: { date: string; count: number }[];
  recentQueries: { question: string; domain: string; status: string; executionMs: number; createdAtUtc: string }[];
}

@Injectable({ providedIn: "root" })
export class DashboardService {
  private readonly base = "http://localhost:5000/api";

  constructor(private http: HttpClient) {}

  getSavedQueries(customerId: string): Observable<SavedQueryDto[]> {
    return this.http.get<SavedQueryDto[]>(`${this.base}/dashboard/saved`, {
      params: { customerId },
    });
  }

  saveQuery(request: SaveQueryRequest, customerId: string): Observable<SavedQueryDto> {
    return this.http.post<SavedQueryDto>(`${this.base}/dashboard/saved`, request, {
      params: { customerId },
    });
  }

  deleteSavedQuery(id: number, customerId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/dashboard/saved/${id}`, {
      params: { customerId },
    });
  }

  getAnalytics(customerId: string, days = 30): Observable<AnalyticsSummary> {
    return this.http.get<AnalyticsSummary>(`${this.base}/analytics/summary`, {
      params: { customerId, days: String(days) },
    });
  }

  getScheduledReports(customerId: string): Observable<ScheduledReportDto[]> {
    return this.http.get<ScheduledReportDto[]>(`${this.base}/scheduledreports`, {
      params: { customerId },
    });
  }

  createScheduledReport(request: CreateScheduledReportRequest, customerId: string): Observable<ScheduledReportDto> {
    return this.http.post<ScheduledReportDto>(`${this.base}/scheduledreports`, request, {
      params: { customerId },
    });
  }

  deleteScheduledReport(id: number, customerId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/scheduledreports/${id}`, {
      params: { customerId },
    });
  }

  // ─── Widget layout persistence ──────────────────────────────────────────────

  getWidgets(customerId: string): Observable<DashboardWidgetRecord[]> {
    return this.http.get<DashboardWidgetRecord[]>(`${this.base}/dashboardwidgets`, {
      params: { customerId },
    });
  }

  saveWidget(request: SaveDashboardWidgetRequest, customerId: string): Observable<DashboardWidgetRecord> {
    return this.http.post<DashboardWidgetRecord>(`${this.base}/dashboardwidgets`, request, {
      params: { customerId },
    });
  }

  updateWidgetOrder(id: number, sortOrder: number, customerId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/dashboardwidgets/${id}/order`, { sortOrder }, {
      params: { customerId },
    });
  }

  updateWidgetView(id: number, viewType: string, customerId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/dashboardwidgets/${id}/view`, { viewType }, {
      params: { customerId },
    });
  }

  deleteWidget(id: number, customerId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/dashboardwidgets/${id}`, {
      params: { customerId },
    });
  }

  deleteAllWidgets(customerId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/dashboardwidgets`, {
      params: { customerId },
    });
  }

  updateWidgetRefresh(id: number, refreshIntervalMinutes: number | null, customerId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/dashboardwidgets/${id}/refresh`, { refreshIntervalMinutes }, {
      params: { customerId },
    });
  }

  updateWidgetThresholds(id: number, thresholdMin: number | null, thresholdMax: number | null, customerId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/dashboardwidgets/${id}/thresholds`, { thresholdMin, thresholdMax }, {
      params: { customerId },
    });
  }

  // ─── Vacancy & Letting ────────────────────────────────────────────────────

  getVacancies(customerId: string, status?: string): Observable<VacancyDto[]> {
    const params: any = { customerId };
    if (status) params['status'] = status;
    return this.http.get<VacancyDto[]>(`${this.base}/vacancy`, { params });
  }

  getLettingApplications(customerId: string, status?: string): Observable<LettingApplicationDto[]> {
    const params: any = { customerId };
    if (status) params['status'] = status;
    return this.http.get<LettingApplicationDto[]>(`${this.base}/vacancy/applications`, { params });
  }

  // ─── Arrears & Escalation ────────────────────────────────────────────────

  getArrearsSummary(customerId: string): Observable<ArrearsSummaryDto> {
    return this.http.get<ArrearsSummaryDto>(`${this.base}/arrears/summary`, { params: { customerId } });
  }

  getArrearsEscalations(customerId: string): Observable<ArrearsEscalationDto[]> {
    return this.http.get<ArrearsEscalationDto[]>(`${this.base}/arrears/escalations`, { params: { customerId } });
  }

  // ─── Compliance Calendar ─────────────────────────────────────────────────

  getComplianceSummary(customerId: string): Observable<ComplianceSummaryDto> {
    return this.http.get<ComplianceSummaryDto>(`${this.base}/compliance/summary`, { params: { customerId } });
  }

  getComplianceOverdue(customerId: string): Observable<ComplianceItemDto[]> {
    return this.http.get<ComplianceItemDto[]>(`${this.base}/compliance/overdue`, { params: { customerId } });
  }

  getComplianceDueSoon(customerId: string, daysAhead = 30): Observable<ComplianceItemDto[]> {
    return this.http.get<ComplianceItemDto[]>(`${this.base}/compliance/due-soon`, {
      params: { customerId, daysAhead: String(daysAhead) }
    });
  }

  // ─── Trust Ledger ────────────────────────────────────────────────────────

  getTrustLedgerOwners(customerId: string): Observable<TrustLedgerSummaryDto[]> {
    return this.http.get<TrustLedgerSummaryDto[]>(`${this.base}/trust-ledger/owners`, { params: { customerId } });
  }

  getTrustLedgerEntries(customerId: string, ownerId: number, limit = 50): Observable<TrustLedgerEntryDto[]> {
    return this.http.get<TrustLedgerEntryDto[]>(`${this.base}/trust-ledger/owners/${ownerId}/entries`, {
      params: { customerId, limit: String(limit) }
    });
  }

  // ─── PM Tasks ────────────────────────────────────────────────────────────

  getPMTaskSummary(customerId: string, assignedTo?: string): Observable<PMTaskSummaryDto> {
    const params: any = { customerId };
    if (assignedTo) params['assignedTo'] = assignedTo;
    return this.http.get<PMTaskSummaryDto>(`${this.base}/pm-tasks/summary`, { params });
  }

  getPMTasks(customerId: string, assignedTo?: string, status?: string): Observable<PMTaskDto[]> {
    const params: any = { customerId };
    if (assignedTo) params['assignedTo'] = assignedTo;
    if (status) params['status'] = status;
    return this.http.get<PMTaskDto[]>(`${this.base}/pm-tasks`, { params });
  }

  updatePMTaskStatus(taskId: number, status: string, customerId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/pm-tasks/${taskId}/status`, { status }, { params: { customerId } });
  }

  // ─── Lease Renewal ───────────────────────────────────────────────────────

  getLeaseRenewalSummary(customerId: string): Observable<LeaseRenewalSummaryDto> {
    return this.http.get<LeaseRenewalSummaryDto>(`${this.base}/lease-renewal/summary`, { params: { customerId } });
  }

  getLeaseRenewals(customerId: string, outcomeCode?: string): Observable<LeaseRenewalOutcomeDto[]> {
    const params: any = { customerId };
    if (outcomeCode) params['outcomeCode'] = outcomeCode;
    return this.http.get<LeaseRenewalOutcomeDto[]>(`${this.base}/lease-renewal`, { params });
  }
}
