import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable, Subject } from "rxjs";
import { QueryResponse, NlQueryRequest } from "./models";
import { environment } from "../environments/environment";

export interface StreamEvent {
  event: string;
  data: Record<string, unknown>;
}

@Injectable({
  providedIn: "root",
})
export class QueryService {
  private readonly apiUrl = environment.apiUrl;
  private readonly streamUrl = `${environment.apiUrl}/stream`;

  constructor(private http: HttpClient) {}

  executeQuery(
    question: string,
    conversationId?: string,
    customerId?: string,
    role?: string,
  ): Observable<QueryResponse> {
    const request: NlQueryRequest = { question, conversationId, customerId, role };
    return this.http.post<QueryResponse>(this.apiUrl, request);
  }

  streamQuery(
    question: string,
    conversationId?: string,
    customerId?: string,
    role?: string,
  ): Observable<StreamEvent> {
    const subject = new Subject<StreamEvent>();
    const body = JSON.stringify({ question, conversationId, customerId, role });

    fetch(this.streamUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
      body,
    })
      .then(async (response) => {
        if (!response.ok || !response.body) {
          subject.error(new Error(`Stream request failed: ${response.status}`));
          return;
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const frames = buffer.split("\n\n");
          buffer = frames.pop() ?? "";

          for (const frame of frames) {
            if (!frame.trim()) continue;
            const eventLine = frame.match(/^event:\s*(.+)$/m);
            const dataLine = frame.match(/^data:\s*(.+)$/m);
            if (eventLine && dataLine) {
              try {
                subject.next({
                  event: eventLine[1].trim(),
                  data: JSON.parse(dataLine[1].trim()),
                });
              } catch {
                // Malformed frame — skip
              }
            }
          }
        }

        subject.complete();
      })
      .catch((err) => subject.error(err));

    return subject.asObservable();
  }
}
