import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Subscription } from "rxjs";
import { HttpClient } from "@angular/common/http";
import { QueryService, StreamEvent } from "./query.service";
import { QueryResponse, VacancyDto, LettingApplicationDto, ArrearsSummaryDto, ComplianceSummaryDto, TrustLedgerSummaryDto, PMTaskSummaryDto, PMTaskDto, LeaseRenewalSummaryDto } from "./models";
import { DashboardService, SavedQueryDto, AnalyticsSummary, ScheduledReportDto, DashboardWidgetRecord } from "./dashboard.service";
import { ChartWidgetComponent } from "./chart-widget.component";
import { ChartType } from "chart.js";

const ROLE_PRESETS: Record<string, string[]> = {
  PropertyManager: [
    "Which tenancies have arrears?",
    "Show open maintenance jobs",
    "Show active tenancies ending in next 60 days",
    "List upcoming inspections",
  ],
  Owner: [
    "Show total income summary by owner",
    "Show vacant properties in portfolio",
    "Show non-compliant inspection results",
  ],
  Tenant: [
    "Show open maintenance jobs",
    "List upcoming inspections",
  ],
};

const DOMAIN_SUGGESTIONS: string[] = [
  "Which tenancies have arrears?",
  "Show open maintenance jobs older than 30 days",
  "Show active tenancies ending in next 60 days",
  "List upcoming inspections for next month",
  "Show vacant properties in portfolio",
  "List all active contractors",
  "Show non-compliant inspection results",
  "Show total income summary by owner",
  "Which leases are expiring in 90 days?",
  "Show maintenance jobs by category",
  "List properties with no active tenancy",
  "Show rent arrears by property",
];

export interface ConversationTurn {
  question: string;
  response?: QueryResponse;
  error?: string;
  showSql?: boolean;
}

export interface ChartRow { label: string; value: number; pct: number; }

export type WidgetViewType = "kpi" | "table" | "chart" | "line" | "pie" | "donut" | "scatter" | "heatmap" | "anomaly";

export interface HeatmapData {
  rowLabels: string[];
  colLabels: string[];
  cells: { value: number; intensity: number }[][];
}

export interface AnomalyResult {
  anomalies: { label: string; value: number; z_score: number; direction: 'high' | 'low' }[];
  mean: number | null;
  stddev: number | null;
}

export interface DashboardWidget {
  id: string;
  dbId?: number;
  title: string;
  question: string;
  viewType: WidgetViewType;
  response?: QueryResponse;
  loading: boolean;
  error?: string;
  refreshIntervalMinutes?: number;
  thresholdMin?: number;
  thresholdMax?: number;
  showSettings?: boolean;
  anomalyResult?: AnomalyResult;
  anomalyLoading?: boolean;
}

@Component({
  selector: "app-root",
  standalone: true,
  imports: [CommonModule, FormsModule, ChartWidgetComponent],
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

  /** Active tab */
  activeTab: "query" | "dashboard" | "analytics" | "vacancies" | "tasks" | "compliance" | "arrears" | "trust-ledger" | "renewals" = "query";

  // ─── Vacancies & Letting ────────────────────────────────────────────────
  vacancies: VacancyDto[] = [];
  lettingApplications: LettingApplicationDto[] = [];
  vacanciesLoading = false;
  vacancyStatusFilter = '';

  // ─── Arrears & Escalation ────────────────────────────────────────────────
  arrearsSummary: ArrearsSummaryDto | null = null;
  arrearsLoading = false;

  // ─── Compliance Calendar ──────────────────────────────────────────────────
  complianceSummary: ComplianceSummaryDto | null = null;
  complianceLoading = false;

  // ─── Trust Ledger ─────────────────────────────────────────────────────────
  trustLedgerOwners: TrustLedgerSummaryDto[] = [];
  trustLedgerLoading = false;
  selectedTrustOwnerId: number | null = null;

  // ─── PM Tasks ─────────────────────────────────────────────────────────────
  pmTaskSummary: PMTaskSummaryDto | null = null;
  pmTasksLoading = false;
  pmTaskStatusFilter = 'OPEN';

  // ─── Lease Renewals ───────────────────────────────────────────────────────
  renewalSummary: LeaseRenewalSummaryDto | null = null;
  renewalsLoading = false;
  renewalOutcomeFilter = '';

  /** Saved/pinned queries (DB-backed) */
  savedQueries: SavedQueryDto[] = [];
  pinDialogOpen: boolean = false;
  pinName: string = "";

  /** Scheduled reports */
  scheduledReports: ScheduledReportDto[] = [];
  scheduleDialogOpen: boolean = false;
  scheduleForm = { name: "", recipientEmail: "", schedule: "daily", alertCondition: "" };

  /** Analytics */
  analytics: AnalyticsSummary | null = null;
  analyticsLoading: boolean = false;

  /** Conversational dashboard */
  widgets: DashboardWidget[] = [];
  dashboardInput: string = "";
  private dashboardConvId: string = this.newId();

  /** Drag-to-reorder state */
  dragSourceId: string | null = null;
  dragOverId: string | null = null;

  /** Global date filter for dashboard */
  dashboardDateFrom: string = "";
  dashboardDateTo: string = "";

  /** Query suggestions (F8) */
  showSuggestions: boolean = false;
  filteredSuggestions: string[] = [];

  /** History search (F7) */
  historySearch: string = "";

  /** Auto-refresh timers keyed by widget id (F3) */
  private autoRefreshTimers = new Map<string, ReturnType<typeof setInterval>>();

  constructor(
    private queryService: QueryService,
    private dashboardService: DashboardService,
    private http: HttpClient,
  ) {}

  ngOnInit(): void {
    this.loadSavedQueries();
  }

  ngOnDestroy(): void {
    this.streamSub?.unsubscribe();
    this.querySub?.unsubscribe();
    this.autoRefreshTimers.forEach(t => clearInterval(t));
    this.autoRefreshTimers.clear();
  }

  switchTab(tab: string): void {
    this.activeTab = tab as any;
    if (tab === "analytics" && !this.analytics) this.loadAnalytics();
    if (tab === "dashboard") {
      this.loadSavedQueries();
      if (this.widgets.length === 0) this.loadPersistedWidgets();
    }
    if (tab === "vacancies" && !this.vacancies.length) this.loadVacancies();
    if (tab === "arrears" && !this.arrearsSummary) this.loadArrears();
    if (tab === "compliance" && !this.complianceSummary) this.loadCompliance();
    if (tab === "trust-ledger" && !this.trustLedgerOwners.length) this.loadTrustLedger();
    if (tab === "tasks" && !this.pmTaskSummary) this.loadPMTasks();
    if (tab === "renewals" && !this.renewalSummary) this.loadRenewals();
  }

  // ─── PM Domain Loaders ────────────────────────────────────────────────────

  loadVacancies(): void {
    this.vacanciesLoading = true;
    this.dashboardService.getVacancies(this.selectedCustomerId, this.vacancyStatusFilter || undefined).subscribe({
      next: v => { this.vacancies = v; this.vacanciesLoading = false; },
      error: () => { this.vacanciesLoading = false; }
    });
    this.dashboardService.getLettingApplications(this.selectedCustomerId).subscribe({
      next: a => { this.lettingApplications = a; }
    });
  }

  loadArrears(): void {
    this.arrearsLoading = true;
    this.dashboardService.getArrearsSummary(this.selectedCustomerId).subscribe({
      next: s => { this.arrearsSummary = s; this.arrearsLoading = false; },
      error: () => { this.arrearsLoading = false; }
    });
  }

  loadCompliance(): void {
    this.complianceLoading = true;
    this.dashboardService.getComplianceSummary(this.selectedCustomerId).subscribe({
      next: s => { this.complianceSummary = s; this.complianceLoading = false; },
      error: () => { this.complianceLoading = false; }
    });
  }

  loadTrustLedger(): void {
    this.trustLedgerLoading = true;
    this.dashboardService.getTrustLedgerOwners(this.selectedCustomerId).subscribe({
      next: o => { this.trustLedgerOwners = o; this.trustLedgerLoading = false; },
      error: () => { this.trustLedgerLoading = false; }
    });
  }

  loadPMTasks(): void {
    this.pmTasksLoading = true;
    this.dashboardService.getPMTaskSummary(this.selectedCustomerId).subscribe({
      next: s => { this.pmTaskSummary = s; this.pmTasksLoading = false; },
      error: () => { this.pmTasksLoading = false; }
    });
  }

  loadRenewals(): void {
    this.renewalsLoading = true;
    this.dashboardService.getLeaseRenewalSummary(this.selectedCustomerId).subscribe({
      next: s => { this.renewalSummary = s; this.renewalsLoading = false; },
      error: () => { this.renewalsLoading = false; }
    });
  }

  completeTask(task: PMTaskDto): void {
    this.dashboardService.updatePMTaskStatus(task.taskId, 'DONE', this.selectedCustomerId).subscribe({
      next: () => { task.status = 'DONE'; }
    });
  }

  loadPersistedWidgets(): void {
    this.dashboardService.getWidgets(this.selectedCustomerId).subscribe({
      next: (records: DashboardWidgetRecord[]) => {
        records.forEach((rec, idx) => {
          const widget: DashboardWidget = {
            id: this.newId(),
            dbId: rec.id,
            title: rec.title,
            question: rec.question,
            viewType: rec.viewType as WidgetViewType,
            loading: true,
            refreshIntervalMinutes: rec.refreshIntervalMinutes ?? undefined,
            thresholdMin: rec.thresholdMin ?? undefined,
            thresholdMax: rec.thresholdMax ?? undefined,
          };
          // Restart auto-refresh if configured
          if (rec.refreshIntervalMinutes) {
            this.armAutoRefresh(widget, rec.refreshIntervalMinutes);
          }
          // Insert in sort order
          this.widgets = [...this.widgets.slice(0, idx), widget, ...this.widgets.slice(idx)];
          this.queryService
            .executeQuery(rec.question, this.dashboardConvId, this.selectedCustomerId, this.selectedRole)
            .subscribe({
              next: (response: QueryResponse) => {
                widget.response = response;
                widget.loading = false;
                widget.error = response.status !== "ok" ? (response.message || "Query failed") : undefined;
              },
              error: (err: { error?: { message?: string }; message?: string }) => {
                widget.loading = false;
                widget.error = err.error?.message || err.message || "Failed";
              },
            });
        });
      },
      error: () => {},
    });
  }

  clearAllWidgets(): void {
    this.autoRefreshTimers.forEach(t => clearInterval(t));
    this.autoRefreshTimers.clear();
    this.widgets = [];
    this.dashboardService.deleteAllWidgets(this.selectedCustomerId).subscribe({ error: () => {} });
  }

  // ─── Conversational Dashboard ────────────────────────────────────────────────

  processDashboardInput(): void {
    const raw = this.dashboardInput.trim();
    if (!raw) return;
    this.dashboardInput = "";
    const lower = raw.toLowerCase();

    // clear / clear all
    if (/^clear(\s+(all|dashboard))?$/.test(lower)) {
      this.widgets = [];
      return;
    }

    // refresh all
    if (/^refresh\s+all$/.test(lower)) {
      this.widgets.forEach(w => this.refreshWidget(w));
      return;
    }

    // refresh <name>
    const refreshNamed = lower.match(/^refresh\s+(.+)$/);
    if (refreshNamed) {
      const w = this.findWidgetByName(refreshNamed[1]);
      if (w) { this.refreshWidget(w); return; }
    }

    // remove / delete / hide <name>
    const removeMatch = lower.match(/^(remove|delete|hide)\s+(.+)$/);
    if (removeMatch) {
      this.removeWidgetByName(removeMatch[2]);
      return;
    }

    // "show as chart/table/kpi"  → change last widget
    const showAsLast = lower.match(/^show\s+as\s+(chart|table|kpi)$/);
    if (showAsLast) {
      if (this.widgets.length)
        this.widgets[this.widgets.length - 1].viewType = showAsLast[1] as WidgetViewType;
      return;
    }

    // "show <name> as chart/table/kpi"  or  "<name> as chart/table/kpi"
    const namedView = lower.match(/^(?:show\s+)?(.+?)\s+as\s+(chart|table|kpi)$/);
    if (namedView) {
      const w = this.findWidgetByName(namedView[1]);
      if (w) { w.viewType = namedView[2] as WidgetViewType; return; }
    }

    // default: treat as NL query → add widget
    this.addWidget(raw);
  }

  addWidget(question: string): void {
    const effectiveQuestion = question + this.buildDateSuffix();
    const widget: DashboardWidget = {
      id: this.newId(),
      title: question.length > 55 ? question.slice(0, 52) + "…" : question,
      question,
      viewType: "table",
      loading: true,
    };
    this.widgets = [...this.widgets, widget];
    this.queryService
      .executeQuery(effectiveQuestion, this.dashboardConvId, this.selectedCustomerId, this.selectedRole)
      .subscribe({
        next: (response: QueryResponse) => {
          widget.response = response;
          widget.loading = false;
          if (response.status === "ok") {
            // Default to table; user can switch view type manually
            widget.viewType = "table";
            // Auto-save to DB (only if not already persisted)
            if (!widget.dbId) {
              this.dashboardService.saveWidget(
                { title: widget.title, question: widget.question,
                  viewType: widget.viewType, sortOrder: this.widgets.indexOf(widget) },
                this.selectedCustomerId
              ).subscribe({
                next: (rec: DashboardWidgetRecord) => { widget.dbId = rec.id; },
                error: () => {},
              });
            }
          } else {
            widget.error = response.message || "Query failed";
          }
        },
        error: (err: { error?: { message?: string }; message?: string }) => {
          widget.loading = false;
          widget.error = err.error?.message || err.message || "Failed";
        },
      });
  }

  refreshWidget(widget: DashboardWidget): void {
    widget.loading = true;
    widget.error = undefined;
    this.queryService
      .executeQuery(widget.question, this.dashboardConvId, this.selectedCustomerId, this.selectedRole)
      .subscribe({
        next: (response: QueryResponse) => {
          widget.response = response;
          widget.loading = false;
          widget.error = response.status !== "ok" ? (response.message || "Query failed") : undefined;
        },
        error: (err: { error?: { message?: string }; message?: string }) => {
          widget.loading = false;
          widget.error = err.error?.message || err.message || "Failed";
        },
      });
  }

  removeWidget(id: string): void {
    const timer = this.autoRefreshTimers.get(id);
    if (timer) { clearInterval(timer); this.autoRefreshTimers.delete(id); }
    const widget = this.widgets.find(w => w.id === id);
    if (widget?.dbId) {
      this.dashboardService.deleteWidget(widget.dbId, this.selectedCustomerId).subscribe({ error: () => {} });
    }
    this.widgets = this.widgets.filter(w => w.id !== id);
  }

  setWidgetView(widget: DashboardWidget, type: WidgetViewType): void {
    widget.viewType = type;
    if (type === 'anomaly' && !widget.anomalyResult && !widget.anomalyLoading) {
      this.runAnomalyDetection(widget);
    }
    if (widget.dbId) {
      this.dashboardService.updateWidgetView(widget.dbId, type, this.selectedCustomerId).subscribe({ error: () => {} });
    }
  }

  detectWidgetType(response: QueryResponse): WidgetViewType {
    const keys = this.getColumnKeys(response);
    const rows = response.rows ?? [];
    if (rows.length === 1 && keys.length === 1) return "kpi";
    // 2+ numeric cols → scatter
    const numCols = keys.filter(k => rows.every(r => !isNaN(Number(r[k])) && r[k] !== null));
    if (numCols.length >= 2 && keys.length === numCols.length) return "scatter";
    // Chartable: 2-8 rows → donut; more → bar
    const chartRows = this.getChartData(response) ?? [];
    if (chartRows.length > 0) return rows.length <= 8 ? "donut" : "chart";
    return "table";
  }

  getWidgetKpi(widget: DashboardWidget): string {
    if (!widget.response?.rows?.length) return "—";
    const keys = this.getColumnKeys(widget.response);
    const val = widget.response.rows[0][keys[0]];
    return val != null ? String(val) : "—";
  }

  drillDown(widget: DashboardWidget): void {
    this.activeTab = "query";
    this.loadExample(widget.question);
    this.runQuery(widget.question);
  }

  pinWidget(widget: DashboardWidget): void {
    this.dashboardService.saveQuery(
      { name: widget.title, question: widget.question, role: this.selectedRole },
      this.selectedCustomerId
    ).subscribe({
      next: (saved: SavedQueryDto) => { this.savedQueries = [saved, ...this.savedQueries]; },
      error: () => {},
    });
  }

  addSavedAsWidget(q: SavedQueryDto): void {
    this.addWidget(q.question);
  }

  // ─── F1: Clone widget ─────────────────────────────────────────────────────────

  cloneWidget(w: DashboardWidget): void {
    this.addWidget(w.question);
  }

  // ─── F2: Export conversation to markdown ─────────────────────────────────────

  exportConversationMarkdown(): void {
    const lines: string[] = [`# Conversation Export\n_Exported: ${new Date().toLocaleString()}_\n`];
    const allTurns = [...this.conversationHistory];
    if (this.response || this.error) {
      allTurns.push({ question: this.question, response: this.response, error: this.error });
    }
    for (const turn of allTurns) {
      lines.push(`## Q: ${turn.question}`);
      if (turn.error) { lines.push(`**Error:** ${turn.error}`); lines.push(''); continue; }
      if (turn.response?.status === 'ok') {
        if (turn.response.nlSummary) lines.push(`**Summary:** ${turn.response.nlSummary}`);
        lines.push(`**Rows:** ${turn.response.rowCount}`);
        const cols = this.getColumnKeys(turn.response);
        if (cols.length && turn.response.rows?.length) {
          lines.push('');
          lines.push('| ' + cols.join(' | ') + ' |');
          lines.push('| ' + cols.map(() => '---').join(' | ') + ' |');
          (turn.response.rows ?? []).slice(0, 20).forEach(row => {
            lines.push('| ' + cols.map(c => String(row[c] ?? '')).join(' | ') + ' |');
          });
        }
      } else if (turn.response?.message) {
        lines.push(`> ${turn.response.message}`);
      }
      lines.push('');
    }
    const blob = new Blob([lines.join('\n')], { type: 'text/markdown;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = `conversation-${Date.now()}.md`; a.click();
    URL.revokeObjectURL(url);
  }

  // ─── F12: Role-based dashboard presets ───────────────────────────────────────

  loadRolePresets(): void {
    const questions = ROLE_PRESETS[this.selectedRole] ?? ROLE_PRESETS['PropertyManager'];
    questions.forEach(q => this.addWidget(q));
  }

  // ─── F9: Print dashboard ──────────────────────────────────────────────────────

  printDashboard(): void {
    window.print();
  }

  // ─── F6: Global date filter helpers ──────────────────────────────────────────

  private buildDateSuffix(): string {
    if (this.dashboardDateFrom && this.dashboardDateTo)
      return ` between ${this.dashboardDateFrom} and ${this.dashboardDateTo}`;
    if (this.dashboardDateFrom) return ` from ${this.dashboardDateFrom}`;
    if (this.dashboardDateTo) return ` until ${this.dashboardDateTo}`;
    return '';
  }

  applyDateFilter(): void {
    this.widgets.forEach(w => this.refreshWidget(w));
  }

  clearDateFilter(): void {
    this.dashboardDateFrom = '';
    this.dashboardDateTo = '';
  }

  // ─── F7: History search filter ────────────────────────────────────────────────

  get filteredHistory() {
    if (!this.analytics?.recentQueries) return [];
    const q = this.historySearch.toLowerCase();
    return q ? this.analytics.recentQueries.filter(r => r.question.toLowerCase().includes(q)) : this.analytics.recentQueries;
  }

  // ─── F3: Auto-refresh ─────────────────────────────────────────────────────────

  private armAutoRefresh(widget: DashboardWidget, minutes: number): void {
    const existing = this.autoRefreshTimers.get(widget.id);
    if (existing) clearInterval(existing);
    const timer = setInterval(() => this.refreshWidget(widget), minutes * 60 * 1000);
    this.autoRefreshTimers.set(widget.id, timer);
  }

  setWidgetAutoRefresh(w: DashboardWidget, minutes: number): void {
    const existing = this.autoRefreshTimers.get(w.id);
    if (existing) { clearInterval(existing); this.autoRefreshTimers.delete(w.id); }
    w.refreshIntervalMinutes = minutes > 0 ? minutes : undefined;
    if (minutes > 0) this.armAutoRefresh(w, minutes);
    if (w.dbId) {
      this.dashboardService.updateWidgetRefresh(w.dbId, minutes > 0 ? minutes : null, this.selectedCustomerId)
        .subscribe({ error: () => {} });
    }
  }

  // ─── F4: KPI threshold alert class ───────────────────────────────────────────

  getKpiAlertClass(w: DashboardWidget): string {
    if (!w.response?.rows?.length) return '';
    const keys = this.getColumnKeys(w.response);
    const raw = w.response.rows[0][keys[0]];
    const val = Number(raw);
    if (isNaN(val)) return '';
    if (w.thresholdMin !== undefined && val < w.thresholdMin) return 'pm-kpi--below';
    if (w.thresholdMax !== undefined && val > w.thresholdMax) return 'pm-kpi--above';
    return 'pm-kpi--ok';
  }

  saveWidgetThresholds(w: DashboardWidget, min: string, max: string): void {
    w.thresholdMin = min !== '' ? Number(min) : undefined;
    w.thresholdMax = max !== '' ? Number(max) : undefined;
    w.showSettings = false;
    if (w.dbId) {
      this.dashboardService.updateWidgetThresholds(
        w.dbId, w.thresholdMin ?? null, w.thresholdMax ?? null, this.selectedCustomerId
      ).subscribe({ error: () => {} });
    }
  }

  // ─── F8: Suggestions ──────────────────────────────────────────────────────────

  onDashboardInputFocus(): void {
    this.updateSuggestions(this.dashboardInput);
    this.showSuggestions = true;
  }

  onDashboardInputChange(): void {
    this.updateSuggestions(this.dashboardInput);
    this.showSuggestions = true;
  }

  private updateSuggestions(input: string): void {
    const q = input.toLowerCase().trim();
    this.filteredSuggestions = q.length < 2
      ? DOMAIN_SUGGESTIONS.slice(0, 6)
      : DOMAIN_SUGGESTIONS.filter(s => s.toLowerCase().includes(q)).slice(0, 6);
  }

  selectSuggestion(s: string): void {
    this.dashboardInput = s;
    this.showSuggestions = false;
    this.processDashboardInput();
  }

  hideSuggestions(): void {
    setTimeout(() => { this.showSuggestions = false; }, 150);
  }

  // ─── F5: Drill-through from chart click ──────────────────────────────────────

  onChartBarClick(widget: DashboardWidget, label: string): void {
    const drillQuestion = `${widget.question} where ${label}`;
    this.addWidget(drillQuestion);
  }

  // ─── F11: Anomaly detection ───────────────────────────────────────────────────

  runAnomalyDetection(w: DashboardWidget): void {
    if (!w.response?.rows?.length) return;
    const keys = this.getColumnKeys(w.response);
    const isNumericCol = (col: string) =>
      w.response!.rows!.every(r => r[col] !== null && r[col] !== undefined && !isNaN(Number(r[col])));
    const valueKey = keys.find(k => isNumericCol(k)) ?? '';
    const labelKey = keys.find(k => k !== valueKey) ?? '';
    if (!valueKey) return;

    w.anomalyLoading = true;
    w.anomalyResult = undefined;
    this.http.post<AnomalyResult>('http://localhost:8000/v1/nl2sql/anomalies', {
      rows: w.response.rows,
      valueKey,
      labelKey,
    }).subscribe({
      next: (result) => { w.anomalyResult = result; w.anomalyLoading = false; },
      error: () => { w.anomalyLoading = false; },
    });
  }

  private removeWidgetByName(name: string): void {
    const w = this.findWidgetByName(name);
    if (w) this.removeWidget(w.id);
  }

  private findWidgetByName(name: string): DashboardWidget | undefined {
    const lower = name.toLowerCase();
    return this.widgets.find(
      w => w.title.toLowerCase().includes(lower) || w.question.toLowerCase().includes(lower)
    );
  }

  // ─── Drag-to-reorder ────────────────────────────────────────────────────────

  onDragStart(id: string, event: DragEvent): void {
    this.dragSourceId = id;
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = "move";
      event.dataTransfer.setData("text/plain", id);
    }
  }

  onDragOver(id: string, event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = "move";
    this.dragOverId = id;
  }

  onDrop(targetId: string, event: DragEvent): void {
    event.preventDefault();
    if (!this.dragSourceId || this.dragSourceId === targetId) {
      this.dragSourceId = null; this.dragOverId = null; return;
    }
    const from = this.widgets.findIndex(w => w.id === this.dragSourceId);
    const to   = this.widgets.findIndex(w => w.id === targetId);
    const reordered = [...this.widgets];
    const [moved] = reordered.splice(from, 1);
    reordered.splice(to, 0, moved);
    this.widgets = reordered;
    this.dragSourceId = null;
    this.dragOverId = null;
    // Persist new sort orders
    reordered.forEach((w, idx) => {
      if (w.dbId) {
        this.dashboardService.updateWidgetOrder(w.dbId, idx, this.selectedCustomerId)
          .subscribe({ error: () => {} });
      }
    });
  }

  onDragEnd(): void {
    this.dragSourceId = null;
    this.dragOverId = null;
  }

  // ─── Chart.js data preparation ───────────────────────────────────────────────

  isChartJsType(type: WidgetViewType): boolean {
    return ["chart", "line", "pie", "donut", "scatter"].includes(type);
  }

  toChartJsType(type: WidgetViewType): ChartType {
    const map: Record<string, ChartType> = {
      chart: "bar", line: "line", pie: "pie", donut: "doughnut", scatter: "scatter",
    };
    return map[type] ?? "bar";
  }

  getChartJsData(response: QueryResponse | undefined, type: WidgetViewType): unknown {
    if (!response?.rows?.length) return null;
    const keys = this.getColumnKeys(response);

    if (type === "scatter") {
      const numCols = keys.filter(k =>
        response.rows!.every(r => r[k] !== null && r[k] !== undefined && !isNaN(Number(r[k])))
      );
      if (numCols.length < 2) return null;
      return {
        datasets: [{
          label: `${numCols[0]} vs ${numCols[1]}`,
          data: response.rows.map(r => ({ x: Number(r[numCols[0]]), y: Number(r[numCols[1]]) })),
          backgroundColor: "rgba(30,125,200,0.65)",
          pointRadius: 5,
        }],
      };
    }

    const chartRows = this.getChartData(response);
    if (!chartRows?.length) return null;

    const palette = chartRows.map((_, i) => `hsl(${(i * 47 + 200) % 360},60%,55%)`);

    if (type === "pie" || type === "donut") {
      return {
        labels: chartRows.map(r => r.label),
        datasets: [{ data: chartRows.map(r => r.value), backgroundColor: palette, borderWidth: 1 }],
      };
    }

    // bar (chart) or line
    return {
      labels: chartRows.map(r => r.label),
      datasets: [{
        label: keys.find(k => response.rows!.every(r => !isNaN(Number(r[k])))) ?? "Value",
        data: chartRows.map(r => r.value),
        backgroundColor: type === "line" ? "rgba(30,125,200,0.15)" : palette,
        borderColor: type === "line" ? "#1e7dc8" : palette,
        borderWidth: type === "line" ? 2 : 1,
        tension: 0.3,
        fill: type === "line",
        pointRadius: type === "line" ? 3 : 0,
      }],
    };
  }

  // ─── Heatmap ─────────────────────────────────────────────────────────────────

  getHeatmapData(response: QueryResponse | undefined): HeatmapData | null {
    if (!response?.rows?.length) return null;
    const keys = this.getColumnKeys(response);
    if (keys.length < 3) return null;

    const [rowKey, colKey, valKey] = keys;
    if (!response.rows.every(r => !isNaN(Number(r[valKey])))) return null;

    const rowLabels = [...new Set(response.rows.map(r => String(r[rowKey] ?? "")))];
    const colLabels = [...new Set(response.rows.map(r => String(r[colKey] ?? "")))];
    const valueMap = new Map<string, number>();
    response.rows.forEach(r => valueMap.set(`${r[rowKey]}|||${r[colKey]}`, Number(r[valKey])));

    const maxVal = Math.max(...[...valueMap.values()], 1);
    const cells = rowLabels.map(row =>
      colLabels.map(col => {
        const value = valueMap.get(`${row}|||${col}`) ?? 0;
        return { value, intensity: Math.round((value / maxVal) * 100) };
      })
    );
    return { rowLabels, colLabels, cells };
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
    this.scheduleForm = { name: this.question.trim().slice(0, 60), recipientEmail: "", schedule: "daily", alertCondition: "" };
    this.scheduleDialogOpen = true;
  }

  confirmSchedule(): void {
    const f = this.scheduleForm;
    if (!f.name.trim() || !f.recipientEmail.trim()) return;
    this.dashboardService.createScheduledReport(
      { name: f.name.trim(), question: this.question.trim(), role: this.selectedRole,
        recipientEmail: f.recipientEmail.trim(), schedule: f.schedule,
        alertCondition: f.alertCondition.trim() || undefined },
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

    const isNumericCol = (col: string) =>
      r.rows!.every(row => row[col] !== null && row[col] !== undefined && row[col] !== "" && !isNaN(Number(row[col])));

    // Label: first column where values are NOT all numeric
    let labelCol = cols.find(col => !isNumericCol(col)) ?? "";
    // Value: first numeric column that isn't the label column
    let valueCol = cols.find(col => col !== labelCol && isNumericCol(col)) ?? "";

    // Fallback: use first col as label, second as value (e.g. year, count)
    if (!labelCol || !valueCol) {
      labelCol = cols[0];
      valueCol = cols.find(col => col !== labelCol && isNumericCol(col)) ?? cols[1];
    }

    if (!labelCol || !valueCol || labelCol === valueCol) return null;

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
