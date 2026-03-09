import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

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
}
