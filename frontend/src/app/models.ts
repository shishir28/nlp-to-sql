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
}

export interface NlQueryRequest {
  question: string;
  conversationId?: string;
}
