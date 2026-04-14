import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';

export interface ItemNota {
  id: number;
  codigoProduto: string;
  descricaoProduto: string;
  quantidade: number;
  valorUnitario: number;
  subtotal: number;
}

export interface NotaFiscal {
  id: number;
  numero: number;
  dataEmissao: string;
  status: 'Aberta' | 'Processando' | 'Fechada' | 'Cancelada';
  valorTotal: number;
  itens: ItemNota[];
}

export interface ImprimirResponse {
  nota: NotaFiscal;
  estoqueOnline: boolean;
  mensagem: string;
}

export interface NotaAtualizadaEvent {
  id: number;
  numero: number;
  status: string;
  mensagem: string;
}

export interface CriarNotaRequest {
  itens: {
    codigoProduto: string;
    descricaoProduto: string;
    quantidade: number;
    valorUnitario: number;
  }[];
}

@Injectable({ providedIn: 'root' })
export class NotaFiscalService implements OnDestroy {
  private readonly baseUrl     = 'http://localhost:5002/api/notasfiscais';
  private readonly hubUrl      = 'http://localhost:5002/hubs/notafiscal';
  private hubConnection!: signalR.HubConnection;

  /** Emite quando o backend (via SignalR) notifica que uma nota foi atualizada. */
  notaAtualizada$ = new Subject<NotaAtualizadaEvent>();

  constructor(private http: HttpClient) {
    this.iniciarSignalR();
  }

  private iniciarSignalR(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection.on('NotaAtualizada', (evt: NotaAtualizadaEvent) => {
      this.notaAtualizada$.next(evt);
    });

    this.hubConnection.start()
      .then(() => console.log('[SignalR] Conectado ao hub de notas fiscais.'))
      .catch(err => console.warn('[SignalR] Falha ao conectar (tentará reconectar):', err));
  }

  ngOnDestroy(): void {
    this.hubConnection?.stop();
    this.notaAtualizada$.complete();
  }

  listar(): Observable<NotaFiscal[]> {
    return this.http.get<NotaFiscal[]>(this.baseUrl);
  }

  obterPorId(id: number): Observable<NotaFiscal> {
    return this.http.get<NotaFiscal>(`${this.baseUrl}/${id}`);
  }

  criar(r: CriarNotaRequest): Observable<NotaFiscal> {
    return this.http.post<NotaFiscal>(this.baseUrl, r);
  }

  /** Retorna 202 Accepted com ImprimirResponse — processamento é assíncrono via RabbitMQ. */
  imprimir(id: number): Observable<ImprimirResponse> {
    return this.http.post<ImprimirResponse>(`${this.baseUrl}/${id}/imprimir`, {});
  }

  cancelar(id: number): Observable<NotaFiscal> {
    return this.http.post<NotaFiscal>(`${this.baseUrl}/${id}/cancelar`, {});
  }
}
