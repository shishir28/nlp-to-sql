import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { QueryResponse, NlQueryRequest } from "./models";
import { environment } from "../environments/environment";

@Injectable({
  providedIn: "root",
})
export class QueryService {
  private readonly apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  executeQuery(
    question: string,
    conversationId?: string,
  ): Observable<QueryResponse> {
    const request: NlQueryRequest = {
      question,
      conversationId,
    };
    return this.http.post<QueryResponse>(this.apiUrl, request);
  }
}
