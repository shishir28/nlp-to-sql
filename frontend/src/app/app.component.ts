import { Component } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { QueryService } from "./query.service";
import { QueryResponse } from "./models";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent {
  question: string = "";
  loading: boolean = false;
  response?: QueryResponse;
  error?: string;

  constructor(private queryService: QueryService) {}

  runQuery(): void {
    if (!this.question.trim()) {
      this.error = "Please enter a question";
      return;
    }

    this.loading = true;
    this.error = undefined;
    this.response = undefined;

    this.queryService.executeQuery(this.question).subscribe({
      next: (response) => {
        this.response = response;
        this.loading = false;
        this.error = this.getErrorForStatus(response);
      },
      error: (err) => {
        this.error =
          err.error?.message || err.message || "Failed to execute query";
        this.loading = false;
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
  }

  loadExample(example: string): void {
    this.question = example;
    this.clearResults();
  }

  isClarificationResponse(): boolean {
    return this.response?.status === "clarification_needed";
  }

  private getErrorForStatus(response: QueryResponse): string | undefined {
    if (response.status === "ok" || response.status === "clarification_needed") {
      return undefined;
    }

    return response.message || "Query failed";
  }
}
