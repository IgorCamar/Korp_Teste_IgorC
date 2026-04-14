import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface InsightResponse {
  analise: string;
  totalProdutos: number;
  produtosBaixo: number;
  produtosEsgotados: number;
  geradoEm: string;
}

@Injectable({ providedIn: 'root' })
export class InsightsService {
  private readonly baseUrl = 'http://localhost:5001/api/produtos';

  constructor(private http: HttpClient) {}

  analisarEstoque(): Observable<InsightResponse> {
    return this.http.get<InsightResponse>(`${this.baseUrl}/insights-ia`);
  }
}
