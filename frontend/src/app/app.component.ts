import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Subscription } from "rxjs";
import { QueryService, StreamEvent } from "./query.service";
import { QueryResponse } from "./models";
import { DashboardService, SavedQueryDto, AnalyticsSummary, ScheduledReportDto } from "./dashboard.service";

export interface ConversationTurn {
  question: string;
  response?: QueryResponse;
  error?: string;
  showSql?: boolean;
}

export interface ChartRow { label: string; value: number; pct: number; }

@Component({
  selector: "app-root",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent implements OnInit, OnDestroy {
  question: string = "";
  loading: boolean = false;
  response?: QueryResponse;
  error?: string;
  streamingAgent: string = "";

  /** SQL panel for current (latest) result */
  showSql: boolean = false;

  /** Inline clarification reply */
  clarificationAnswer: string = "";

  /** Auto-chart toggle */
  showChart: boolean = false;

  /** Conversation thread */
  conversationId: string = this.newId();
  conversationHistory: ConversationTurn[] = [];

  /** Role/customer switcher */
  selectedRole: string = "PropertyManager";
  selectedCustomerId: string = "1";

  readonly roles = ["PropertyManager", "Owner", "Tenant"];
  readonly customerIds = ["1", "2", "3", "4", "5"];

  private streamSub?: Subscription;
  private querySub?: Subscription;

  readonly examples = [
    { label: "Expiring Leases",   query: "Show active tenancies ending in next 60 days" },
    { label: "Arrears",           query: "Which tenancies have arrears?" },
    { label: "Open Jobs",         query: "Show open maintenance jobs" },
    { label: "Inspections",       query: "List upcoming inspections" },
    { label: "Contractors",       query: "List all active contractors" },
    { label: "Vacant Properties", query: "Show vacant properties in portfolio" },
    { label: "Lease Renewals",    query: "Which leases are expiring in 90 days?" },
    { label: "Compliance Fails",  query: "Show non-compliant inspection results" },
    { label: "Financial Summary", query: "Show total income summary by owner" },
  ];

  /** Phase 3: active tab */
  activeTab: "query" | "dashboard" | "analytics" = "query";

  /** Phase 3: saved/pinned queries */
  savedQueries: SavedQueryDto[] = [];
  pinDialogOpen: boolean = false;
  pinName: string = "";

  /** Phase 3: scheduled reports */
  scheduledReports: ScheduledReportDto[] = [];
  scheduleDialogOpen: boolean = false;
  scheduleForm = { name: "", recipientEmail: "", schedule: "daily" };

  /** Phase 3: analytics */
  analytics: AnalyticsSummary | null = null;
  analyticsLoading: boolean = false;

  constructor(private queryService: QueryService, private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.loadSavedQueries();
  }

  ngOnDestroy(): void {
    this.streamSub?.unsubscribe();
    this.querySub?.unsubscribe();
  }

  /** Phase 3: tab navigation */
  switchTab(tab: "query" | "dashboard" | "analytics"): void {
    this.activeTab = tab;
    if (tab === "analytics" && !this.analytics) {
      this.loadAnalytics();
    }
    if (tab === "dashboard") {
      this.loadSavedQueries();
      this.loadScheduledReports();
    }
  }

  /** Phase 3: pin current query */
  openPinDialog(): void {
    this.pinName = this.question.trim().slice(0, 60);
    this.pinDialogOpen = true;
  }

  confirmPin(): void {
    if (!this.pinName.trim() || !this.question.trim()) return;
    this.dashboardService.saveQuery(
      { name: this.pinName.trim(), question: this.question.trim(), role: this.selectedRole },
      this.selectedCustomerId
    ).subscribe({
      next: (saved: SavedQueryDto) => {
        this.savedQueries = [saved, ...this.savedQueries];
        this.pinDialogOpen = false;
        this.pinName = "";
      },
      error: () => { this.pinDialogOpen = false; }
    });
  }

  cancelPin(): void {
    this.pinDialogOpen = false;
    this.pinName = "";
  }

  loadSavedQueries(): void {
    this.dashboardService.getSavedQueries(this.selectedCustomerId).subscribe({
      next: (q: SavedQueryDto[]) => { this.savedQueries = q; },
      error: () => {}
    });
  }

  deleteSavedQuery(id: number): void {
    this.dashboardService.deleteSavedQuery(id, this.selectedCustomerId).subscribe({
      next: () => { this.savedQueries = this.savedQueries.filter(q => q.id !== id); },
      error: () => {}
    });
  }

  runSavedQuery(q: SavedQueryDto): void {
    this.activeTab = "query";
    this.loadExample(q.question);
    this.runQuery(q.question);
  }

  /** Phase 3: scheduled reports */
  openScheduleDialog(): void {
    this.scheduleForm = { name: this.question.trim().slice(0, 60), recipientEmail: "", schedule: "daily" };
    this.scheduleDialogOpen = true;
  }

  confirmSchedule(): void {
    const f = this.scheduleForm;
    if (!f.name.trim() || !f.recipientEmail.trim()) return;
    this.dashboardService.createScheduledReport(
      { name: f.name.trim(), question: this.question.trim(), role: this.selectedRole,
        recipientEmail: f.recipientEmail.trim(), schedule: f.schedule },
      this.selectedCustomerId
    ).subscribe({
      next: (r: ScheduledReportDto) => {
        this.scheduledReports = [r, ...this.scheduledReports];
        this.scheduleDialogOpen = false;
      },
      error: () => { this.scheduleDialogOpen = false; }
    });
  }

  cancelSchedule(): void {
    this.scheduleDialogOpen = false;
  }

  loadScheduledReports(): void {
    this.dashboardService.getScheduledReports(this.selectedCustomerId).subscribe({
      next: (r: ScheduledReportDto[]) => { this.scheduledReports = r; },
      error: () => {}
    });
  }

  deleteScheduledReport(id: number): void {
    this.dashboardService.deleteScheduledReport(id, this.selectedCustomerId).subscribe({
      next: () => { this.scheduledReports = this.scheduledReports.filter(r => r.id !== id); },
      error: () => {}
    });
  }

  /** Phase 3: analytics */
  loadAnalytics(): void {
    this.analyticsLoading = true;
    this.dashboardService.getAnalytics(this.selectedCustomerId).subscribe({
      next: (a: AnalyticsSummary) => { this.analytics = a; this.analyticsLoading = false; },
      error: () => { this.analyticsLoading = false; }
    });
  }

  getAnalyticsMaxDomain(): number {
    if (!this.analytics?.domainBreakdown?.length) return 1;
    return Math.max(...this.analytics.domainBreakdown.map(d => d.count));
  }

  runQuery(questionOverride?: string): void {
    const q = (questionOverride ?? this.question).trim();
    if (!q) { this.error = "Please enter a question"; return; }

    // Archive current response to history before starting new query
    if (this.response || this.error) {
      this.conversationHistory.push({
        question: this.question,
        response: this.response,
        error: this.error,
        showSql: false,
      });
    }

    this.question = questionOverride ?? this.question;
    this.loading = true;
    this.streamingAgent = "";
    this.showSql = false;
    this.showChart = false;
    this.clarificationAnswer = "";
    this.error = undefined;
    this.response = undefined;
    this.streamSub?.unsubscribe();
    this.querySub?.unsubscribe();

    this.streamSub = this.queryService
      .streamQuery(q, this.conversationId, this.selectedCustomerId, this.selectedRole)
      .subscribe({
        next: (evt: StreamEvent) => {
          if (evt.event === "agent_start") {
            this.streamingAgent = this.formatAgentLabel((evt.data["agent"] as string) ?? "");
          } else if (evt.event === "done" || evt.event === "error") {
            this.streamingAgent = "";
          }
        },
        error: () => { this.streamingAgent = ""; },
      });

    this.querySub = this.queryService
      .executeQuery(q, this.conversationId, this.selectedCustomerId, this.selectedRole)
      .subscribe({
        next: (response: QueryResponse) => {
          this.response = response;
          this.loading = false;
          this.streamingAgent = "";
          this.error = this.getErrorForStatus(response);
          this.showChart = this.hasChartableData();
        },
        error: (err: { error?: { message?: string }; message?: string }) => {
          this.error = err.error?.message || err.message || "Failed to execute query";
          this.loading = false;
          this.streamingAgent = "";
        },
      });
  }

  /** Feature 5: submit clarification inline */
  submitClarification(): void {
    if (!this.clarificationAnswer.trim()) return;
    // Archive the clarification prompt turn
    this.conversationHistory.push({
      question: this.question,
      response: this.response,
      error: this.error,
      showSql: false,
    });
    const answer = this.clarificationAnswer.trim();
    this.question = answer;
    this.clarificationAnswer = "";
    this.response = undefined;
    this.error = undefined;
    this.runQuery(answer);
  }

  /** Feature 2: CSV export */
  exportCsv(response?: QueryResponse): void {
    const r = response ?? this.response;
    if (!r?.rows?.length) return;
    const cols = this.getColumnKeys(r);
    const header = cols.join(",");
    const rowLines = r.rows.map((row) =>
      cols.map((c) => {
        const val = row[c] ?? "";
        const str = String(val);
        return str.includes(",") || str.includes('"') || str.includes("\n")
          ? `"${str.replace(/"/g, '""')}"` : str;
      }).join(",")
    );
    const csv = [header, ...rowLines].join("\n");
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `query-results-${Date.now()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  /** Feature 6: auto-detect chartable data */
  getChartData(response?: QueryResponse): ChartRow[] | null {
    const r = response ?? this.response;
    if (!r?.rows?.length || r.rows.length < 2) return null;
    const cols = this.getColumnKeys(r);
    if (cols.length < 2) return null;

    const sample = r.rows[0];
    let labelCol = "";
    let valueCol = "";

    for (const col of cols) {
      const val = sample[col];
      if (!labelCol && (typeof val === "string") && isNaN(Number(val))) {
        labelCol = col;
      }
      if (!valueCol && (typeof val === "number" || (typeof val === "string" && !isNaN(Number(val)) && val !== ""))) {
        if (r.rows.every((row) => !isNaN(Number(row[col])))) {
          valueCol = col;
        }
      }
    }

    if (!labelCol || !valueCol) return null;

    const data = r.rows.slice(0, 15).map((row) => ({
      label: String(row[labelCol] ?? ""),
      value: Number(row[valueCol]) || 0,
    }));

    const max = Math.max(...data.map((d) => d.value));
    if (max <= 0) return null;

    return data.map((d) => ({ ...d, pct: Math.round((d.value / max) * 100) }));
  }

  hasChartableData(): boolean {
    return (this.getChartData() ?? []).length > 0;
  }

  getColumnKeys(r?: QueryResponse): string[] {
    const resp = r ?? this.response;
    if (!resp?.columns?.length) {
      return resp?.rows?.[0] ? Object.keys(resp.rows[0]) : [];
    }
    return resp.columns.map((col) => col.name);
  }

  newConversation(): void {
    this.conversationHistory = [];
    this.conversationId = this.newId();
    this.response = undefined;
    this.error = undefined;
    this.question = "";
    this.streamingAgent = "";
    this.showSql = false;
    this.showChart = false;
    this.clarificationAnswer = "";
    this.streamSub?.unsubscribe();
    this.querySub?.unsubscribe();
  }

  clearResults(): void {
    this.response = undefined;
    this.error = undefined;
    this.streamingAgent = "";
    this.showSql = false;
    this.showChart = false;
    this.streamSub?.unsubscribe();
    this.querySub?.unsubscribe();
  }

  loadExample(example: string): void {
    this.question = example;
    this.clearResults();
  }

  isClarificationResponse(): boolean {
    return this.response?.status === "clarification_needed";
  }

  private formatAgentLabel(agent: string): string {
    const labels: Record<string, string> = {
      domain_classifier: "Classifying domain...",
      schema_prefetch:   "Loading schema...",
      schema_analyzer:   "Analysing schema...",
      sql_generator:     "Generating SQL...",
      sql_validator:     "Validating SQL...",
      route:    "Routing...",
      schema:   "Loading schema...",
      planner:  "Planning query...",
      generate: "Generating SQL...",
    };
    return labels[agent] ?? `Running ${agent}...`;
  }

  private getErrorForStatus(response: QueryResponse): string | undefined {
    if (response.status === "ok" || response.status === "clarification_needed") return undefined;
    return response.message || "Query failed";
  }

  private newId(): string {
    return typeof crypto !== "undefined" && crypto.randomUUID
      ? crypto.randomUUID()
      : Math.random().toString(36).slice(2);
  }
}
