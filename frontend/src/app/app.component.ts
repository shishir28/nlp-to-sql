import { Component, OnDestroy } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Subscription } from "rxjs";
import { QueryService } from "./query.service";
import { QueryResponse } from "./models";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent implements OnDestroy {
  question: string = "";
  loading: boolean = false;
  response?: QueryResponse;
  error?: string;

  /** Label shown while streaming agent progress (e.g. "Running domain_classifier...") */
  streamingAgent: string = "";

  private streamSub?: Subscription;
  private querySub?: Subscription;

  readonly examples = [
    { label: "Expiring Leases",    query: "Show active tenancies ending in next 60 days" },
    { label: "Arrears",            query: "Which tenancies have arrears?" },
    { label: "Open Jobs",          query: "Show open maintenance jobs" },
    { label: "Inspections",        query: "List upcoming inspections" },
    { label: "Contractors",        query: "List all active contractors" },
    { label: "Vacant Properties",  query: "Show vacant properties in portfolio" },
    { label: "Lease Renewals",     query: "Which leases are expiring in 90 days?" },
    { label: "Compliance Fails",   query: "Show non-compliant inspection results" },
    { label: "Financial Summary",  query: "Show total income summary by owner" },
  ];

  constructor(private queryService: QueryService) {}

  ngOnDestroy(): void {
    this.streamSub?.unsubscribe();
    this.querySub?.unsubscribe();
  }

  runQuery(): void {
    if (!this.question.trim()) {
      this.error = "Please enter a question";
      return;
    }

    this.loading = true;
    this.streamingAgent = "";
    this.error = undefined;
    this.response = undefined;
    this.streamSub?.unsubscribe();
    this.querySub?.unsubscribe();

    // Stream agent progress for live status updates
    this.streamSub = this.queryService.streamQuery(this.question).subscribe({
      next: (evt) => {
        if (evt.event === "agent_start") {
          const agent = (evt.data["agent"] as string) ?? "";
          this.streamingAgent = this.formatAgentLabel(agent);
        } else if (evt.event === "done" || evt.event === "error") {
          this.streamingAgent = "";
        }
      },
      error: () => { this.streamingAgent = ""; },
    });

    // Execute query through the full pipeline (firewall + DB)
    this.querySub = this.queryService.executeQuery(this.question).subscribe({
      next: (response) => {
        this.response = response;
        this.loading = false;
        this.streamingAgent = "";
        this.error = this.getErrorForStatus(response);
      },
      error: (err) => {
        this.error = err.error?.message || err.message || "Failed to execute query";
        this.loading = false;
        this.streamingAgent = "";
      },
    });
  }

  getColumnKeys(): string[] {
    if (!this.response?.columns || this.response.columns.length === 0) {
      return this.response?.rows?.[0] ? Object.keys(this.response.rows[0]) : [];
    }
    return this.response.columns.map((col) => col.name);
  }

  clearResults(): void {
    this.response = undefined;
    this.error = undefined;
    this.streamingAgent = "";
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
    if (response.status === "ok" || response.status === "clarification_needed") {
      return undefined;
    }
    return response.message || "Query failed";
  }
}
