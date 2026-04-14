import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { switchMap, catchError, finalize, tap, delay } from 'rxjs/operators';
import { of } from 'rxjs';
import { NotaFiscalService, NotaFiscal, ImprimirResponse, NotaAtualizadaEvent } from '../services/nota-fiscal.service';

interface Toast {
  title: string;
  message: string;
  type: 'success' | 'error' | 'warning' | 'info';
}

@Component({
  selector: 'app-invoice-list',
  templateUrl: './invoice-list.component.html'
})
export class InvoiceListComponent implements OnInit, OnDestroy {
  notas: NotaFiscal[] = [];
  filtered: NotaFiscal[] = [];
  isLoading  = false;
  printingId: number | null = null;
  cancelingId: number | null = null;
  searchTerm   = '';
  filterStatus: 'all' | 'Aberta' | 'Processando' | 'Fechada' | 'Cancelada' = 'all';
  toast: Toast | null = null;

  // Confirm dialog state
  confirmDialog: {
    visible: boolean;
    title: string;
    message: string;
    detail: string;
    type: 'danger' | 'warning';
    confirmLabel: string;
    action: () => void;
  } = { visible: false, title: '', message: '', detail: '', type: 'danger', confirmLabel: 'Confirmar', action: () => {} };
  private toastTimer: any;
  private printSubject$ = new Subject<number>();
  private subs = new Subscription();

  constructor(private service: NotaFiscalService) {}

  ngOnInit(): void {
    this.carregar();
    this.setupPrintStream();
    this.setupSignalR();
  }

  ngOnDestroy(): void { this.subs.unsubscribe(); }

  carregar(): void {
    this.isLoading = true;
    this.subs.add(
      this.service.listar()
        .pipe(finalize(() => (this.isLoading = false)))
        .subscribe({
          next: n => { this.notas = n; this.applyFilter(); },
          error: () => this.notify('Erro', 'Não foi possível carregar as notas fiscais.', 'error')
        })
    );
  }

  /**
   * Escuta eventos SignalR vindos do backend.
   * Quando o consumer do RabbitMQ confirma o fechamento de uma nota,
   * o backend emite "NotaAtualizada" e aqui atualizamos a lista sem recarregar.
   */
  private setupSignalR(): void {
    this.subs.add(
      this.service.notaAtualizada$.subscribe((evt: NotaAtualizadaEvent) => {
        // Atualiza o status da nota na lista local
        this.notas = this.notas.map(n =>
          n.id === evt.id ? { ...n, status: evt.status as any } : n
        );
        this.applyFilter();

        // Toast de sucesso com a mensagem personalizada do backend
        this.notify(
          'Nota processada com sucesso',
          evt.mensagem,
          'success'
        );

        // Se a nota foi fechada, abre o PDF automaticamente
        const nota = this.notas.find(n => n.id === evt.id);
        if (nota && evt.status === 'Fechada') {
          this.abrirPdf(nota);
        }
      })
    );
  }

  /**
   * Pipeline de impressão assíncrono (RabbitMQ):
   * 1. Delay de 3s (spinner sempre visível)
   * 2. POST /imprimir → 202 Accepted (nota vai para Processando)
   * 3. Toast informativo baseado no estado do Estoque
   * 4. Nota fecha via SignalR quando o consumer confirmar
   */
  private setupPrintStream(): void {
    this.subs.add(
      this.printSubject$.pipe(
        tap(id => { this.printingId = id; this.toast = null; }),
        switchMap(id =>
          of(id).pipe(
            delay(3000),
            switchMap(notaId =>
              this.service.imprimir(notaId).pipe(
                catchError((err: any) => {
                  this.tratarErroImpressao(err);
                  return of(null);
                })
              )
            ),
            finalize(() => (this.printingId = null))
          )
        )
      ).subscribe((result: ImprimirResponse | null) => {
        if (!result) return;

        // Atualiza nota para Processando na UI
        this.notas = this.notas.map(n =>
          n.id === result.nota.id ? { ...n, status: 'Processando' } : n
        );
        this.applyFilter();

        // Toast diferenciado: Estoque online vs offline (degradação graciosa)
        if (result.estoqueOnline) {
          this.notify(
            'Nota enviada para processamento',
            result.mensagem,
            'info'
          );
        } else {
          this.notify(
            'Nota registrada com lentidão no Estoque',
            result.mensagem,
            'warning'
          );
        }
      })
    );
  }

  /**
   * Classifica erros pelo HTTP status e campo "tipo".
   * Nunca expõe termos de infraestrutura ("RabbitMQ", "SQL", etc.) ao usuário.
   */
  private tratarErroImpressao(err: any): void {
    const status = err?.status as number;
    const tipo   = err?.error?.tipo as string ?? '';

    if (status === 0) {
      this.notify(
        'Servidor indisponível',
        'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.',
        'error'
      );
      return;
    }

    if (status === 503 && tipo === 'MENSAGERIA_INDISPONIVEL') {
      this.notify(
        'Falha de comunicação interna',
        'A nota fiscal não pôde ser impressa e seu status foi mantido como Aberta por segurança. ' +
        'Por favor, tente novamente em alguns instantes.',
        'error'
      );
      return;
    }

    if (tipo === 'REGRA_NEGOCIO' || status === 422) {
      this.notify('Operação não permitida', err?.error?.message ?? 'Verifique o status da nota.', 'warning');
      return;
    }

    this.notify('Erro inesperado', 'Tente novamente. Se o problema persistir, contate o suporte.', 'error');
  }

  onImprimir(nota: NotaFiscal): void {
    if (nota.status !== 'Aberta') {
      this.notify('Impressão bloqueada',
        `Apenas notas Abertas podem ser impressas. Status atual: ${nota.status}.`, 'warning');
      return;
    }
    if (this.printingId === nota.id) return;
    this.printSubject$.next(nota.id);
  }

  onCancelar(nota: NotaFiscal): void {
    if (nota.status !== 'Aberta') {
      this.notify('Cancelamento bloqueado',
        `Apenas notas Abertas podem ser canceladas. Status: ${nota.status}.`, 'warning');
      return;
    }

    this.confirmDialog = {
      visible: true,
      title: 'Cancelar Nota Fiscal',
      message: `Deseja cancelar a nota ${this.formatNumero(nota.numero)}? Esta ação não pode ser desfeita.`,
      detail: `Valor: ${nota.valorTotal.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })} · ${nota.itens.length} item(ns)`,
      type: 'danger',
      confirmLabel: 'Cancelar Nota',
      action: () => this.executarCancelamento(nota)
    };
  }

  onConfirmDialogConfirm(): void {
    this.confirmDialog.action();
    this.confirmDialog.visible = false;
  }

  onConfirmDialogCancel(): void {
    this.confirmDialog.visible = false;
  }

  private executarCancelamento(nota: NotaFiscal): void {
    this.cancelingId = nota.id;
    this.subs.add(
      this.service.cancelar(nota.id)
        .pipe(finalize(() => (this.cancelingId = null)))
        .subscribe({
          next: updated => {
            this.notas = this.notas.map(n => n.id === updated.id ? updated : n);
            this.applyFilter();
            this.notify('Nota cancelada', `Nota ${this.formatNumero(updated.numero)} cancelada.`, 'warning');
          },
          error: (err: any) => this.notify('Erro', err?.error?.message ?? 'Erro ao cancelar.', 'error')
        })
    );
  }

  private abrirPdf(nota: NotaFiscal): void {
    const linhas = nota.itens.map((item, i) => `
      <tr style="background:${i % 2 === 0 ? '#fff' : '#f9fafb'}">
        <td style="padding:9px 14px;border-bottom:1px solid #e5e7eb;font-family:monospace;font-size:12px">${item.codigoProduto}</td>
        <td style="padding:9px 14px;border-bottom:1px solid #e5e7eb">${item.descricaoProduto}</td>
        <td style="padding:9px 14px;border-bottom:1px solid #e5e7eb;text-align:right">${Number(item.quantidade).toFixed(2)}</td>
        <td style="padding:9px 14px;border-bottom:1px solid #e5e7eb;text-align:right">${Number(item.valorUnitario).toLocaleString('pt-BR',{style:'currency',currency:'BRL'})}</td>
        <td style="padding:9px 14px;border-bottom:1px solid #e5e7eb;text-align:right;font-weight:600;color:#0F2D52">${Number(item.subtotal).toLocaleString('pt-BR',{style:'currency',currency:'BRL'})}</td>
      </tr>`).join('');

    const html = `<!DOCTYPE html><html lang="pt-BR"><head><meta charset="UTF-8">
<title>NF ${this.formatNumero(nota.numero)}</title>
<style>
  *{margin:0;padding:0;box-sizing:border-box}body{font-family:'Segoe UI',Arial,sans-serif;color:#1a202c;background:#f7f8fa;font-size:13px}
  .page{max-width:820px;margin:32px auto;background:#fff;border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08)}
  .header{background:#0F2D52;color:#fff;padding:28px 36px;display:flex;justify-content:space-between;align-items:center}
  .brand{font-size:22px;font-weight:300;letter-spacing:1px}.brand strong{font-weight:700}
  .brand-sub{font-size:11px;opacity:.5;margin-top:3px}
  .nf-num{text-align:right}.nf-label{font-size:10px;opacity:.55;letter-spacing:.6px;text-transform:uppercase;margin-bottom:4px}
  .nf-val{font-size:28px;font-weight:300;font-family:monospace;letter-spacing:-1px}
  .meta{display:grid;grid-template-columns:repeat(3,1fr);border-bottom:1px solid #e2e8f0}
  .meta-cell{padding:18px 24px;border-right:1px solid #e2e8f0}.meta-cell:last-child{border-right:none}
  .ml{font-size:10px;text-transform:uppercase;letter-spacing:.6px;color:#6b7280;font-weight:600;margin-bottom:5px}.mv{font-size:13px;font-weight:500}
  table{width:100%;border-collapse:collapse;margin-top:8px}
  thead th{background:#f7f8fa;padding:10px 14px;text-align:left;font-size:10.5px;font-weight:600;text-transform:uppercase;letter-spacing:.5px;color:#6b7280;border-bottom:2px solid #e2e8f0}
  .total-row{display:flex;justify-content:flex-end;align-items:baseline;gap:16px;padding:20px 36px;border-top:2px solid #e2e8f0;margin-top:8px}
  .tl{font-size:11px;text-transform:uppercase;letter-spacing:.6px;color:#6b7280}.tv{font-size:28px;font-weight:300;color:#0F2D52;font-family:monospace}
  .footer{background:#f7f8fa;padding:14px 24px;text-align:center;font-size:10.5px;color:#9ca3af;border-top:1px solid #e2e8f0}
  .pb{display:block;text-align:center;margin:20px auto}.pb button{padding:11px 28px;background:#0F2D52;color:#fff;border:none;border-radius:6px;font-size:13px;cursor:pointer}
  @media print{body{background:#fff}.page{margin:0;border:none;border-radius:0;box-shadow:none}.pb{display:none}}
</style></head><body>
<div class="page">
  <div class="header">
    <div><div class="brand"><strong>KORP</strong> ERP</div><div class="brand-sub">Sistema de Faturamento — Documento Simulado</div></div>
    <div class="nf-num"><div class="nf-label">Nota Fiscal</div><div class="nf-val">${this.formatNumero(nota.numero)}</div></div>
  </div>
  <div class="meta">
    <div class="meta-cell"><div class="ml">Data de Emissão</div><div class="mv">${new Date(nota.dataEmissao).toLocaleString('pt-BR')}</div></div>
    <div class="meta-cell"><div class="ml">Status</div><div class="mv">Fechada</div></div>
    <div class="meta-cell"><div class="ml">Itens</div><div class="mv">${nota.itens.length} ${nota.itens.length===1?'item':'itens'}</div></div>
  </div>
  <div style="padding:16px 24px 0;font-size:10px;text-transform:uppercase;letter-spacing:.6px;color:#6b7280;font-weight:600">Itens da Nota</div>
  <table><thead><tr><th>Código</th><th>Descrição</th><th style="text-align:right">Qtd</th><th style="text-align:right">Vlr. Unit.</th><th style="text-align:right">Subtotal</th></tr></thead>
  <tbody>${linhas}</tbody></table>
  <div class="total-row"><span class="tl">Valor Total</span><span class="tv">${Number(nota.valorTotal).toLocaleString('pt-BR',{style:'currency',currency:'BRL'})}</span></div>
  <div class="footer">Emitido em ${new Date().toLocaleString('pt-BR')} — Korp ERP — Documento fictício para fins de demonstração</div>
</div>
<div class="pb"><button onclick="window.print()">Salvar como PDF / Imprimir</button></div>
</body></html>`;

    const blob = new Blob([html], { type: 'text/html' });
    const url  = URL.createObjectURL(blob);
    const win  = window.open(url, '_blank');
    if (win) setTimeout(() => URL.revokeObjectURL(url), 15000);
  }

  onSearch(t: string): void { this.searchTerm = t; this.applyFilter(); }
  onFilterStatus(s: any): void { this.filterStatus = s; this.applyFilter(); }

  applyFilter(): void {
    let list = [...this.notas];
    if (this.filterStatus !== 'all') list = list.filter(n => n.status === this.filterStatus);
    const t = this.searchTerm.toLowerCase().trim();
    if (t) list = list.filter(n => String(n.numero).includes(t));
    this.filtered = list;
  }

  isPrinting(id: number): boolean  { return this.printingId === id; }
  isCanceling(id: number): boolean { return this.cancelingId === id; }
  formatNumero(n: number): string   { return '#' + String(n).padStart(6, '0'); }

  get totalNotas(): number    { return this.notas.length; }
  get totalAbertas(): number  { return this.notas.filter(n => n.status === 'Aberta').length; }
  get totalFechadas(): number { return this.notas.filter(n => n.status === 'Fechada').length; }
  get totalProcessando(): number { return this.notas.filter(n => n.status === 'Processando').length; }
  get valorTotal(): number    { return this.notas.filter(n => n.status === 'Fechada').reduce((a,n) => a + n.valorTotal, 0); }

  private notify(title: string, message: string, type: 'success'|'error'|'warning'|'info'): void {
    clearTimeout(this.toastTimer);
    this.toast = { title, message, type };
    this.toastTimer = setTimeout(() => (this.toast = null), 9000);
  }
  dismissToast(): void { this.toast = null; }
}
