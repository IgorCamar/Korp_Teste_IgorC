import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Produto {
  id: number;
  codigo: string;
  descricao: string;
  saldo: number;
}

export interface CriarProdutoRequest {
  codigo: string;
  descricao: string;
  saldoInicial: number;
}

export interface AtualizarProdutoRequest {
  descricao: string;
}

@Injectable({ providedIn: 'root' })
export class ProdutoService {
  private readonly baseUrl = 'http://localhost:5001/api/produtos';

  constructor(private http: HttpClient) {}

  listar(): Observable<Produto[]> {
    return this.http.get<Produto[]>(this.baseUrl);
  }

  criar(r: CriarProdutoRequest): Observable<Produto> {
    return this.http.post<Produto>(this.baseUrl, r);
  }

  atualizar(id: number, r: AtualizarProdutoRequest): Observable<Produto> {
    return this.http.put<Produto>(`${this.baseUrl}/${id}`, r);
  }

  incrementarSaldo(id: number, quantidade: number): Observable<Produto> {
    return this.http.post<Produto>(`${this.baseUrl}/${id}/incrementar`, { quantidade });
  }

  deletar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
