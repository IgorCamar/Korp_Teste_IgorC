import { Component, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { InsightsService, InsightResponse } from '../services/insights.service';

@Component({
  selector: 'app-ai-insights',
  template: `
    <div class="ai-card" [class.ai-card--loading]="isLoading" [class.ai-card--done]="!!insight">

      <!-- Header sempre visível -->
      <div class="ai-header">
        <div class="ai-header-left">
          <div class="ai-icon">
            <svg viewBox="0 0 20 20" fill="none">
              <path d="M10 2L11.8 7.2H17.3L12.8 10.4L14.6 15.6L10 12.4L5.4 15.6L7.2 10.4L2.7 7.2H8.2L10 2Z"
                    stroke="currentColor" stroke-width="1.5" stroke-linejoin="round"/>
            </svg>
          </div>
          <div>
            <div class="ai-title">Análise de Saúde do Estoque com IA</div>
            <div class="ai-subtitle">Agente RAG — dados reais analisados pelo Claude</div>
          </div>
        </div>
        <button class="btn btn-primary btn-sm ai-btn"
          [disabled]="isLoading"
          (click)="analisar()">
          <span *ngIf="isLoading" class="spinner-ring" style="width:12px;height:12px;border-width:2px"></span>
          <svg *ngIf="!isLoading" viewBox="0 0 12 12" style="width:12px;height:12px;fill:none;stroke:currentColor;stroke-width:1.5">
            <path d="M6 1v10M1 6h10"/>
          </svg>
          {{ isLoading ? 'Analisando...' : (insight ? 'Reanalisar' : 'Analisar Estoque') }}
        </button>
      </div>

      <!-- Loading state -->
      <div *ngIf="isLoading" class="ai-loading">
        <div class="ai-loading-dots">
          <span></span><span></span><span></span>
        </div>
        <div class="ai-loading-text">O agente está recuperando os dados e gerando sua análise...</div>
      </div>

      <!-- Result -->
      <div *ngIf="insight && !isLoading" class="ai-result">

        <!-- KPI chips -->
        <div class="ai-chips">
          <div class="ai-chip ai-chip--neutral">
            <span class="ai-chip-val">{{ insight.totalProdutos }}</span>
            <span class="ai-chip-lbl">Produtos analisados</span>
          </div>
          <div class="ai-chip" [ngClass]="insight.produtosBaixo > 0 ? 'ai-chip--warn' : 'ai-chip--ok'">
            <span class="ai-chip-val">{{ insight.produtosBaixo }}</span>
            <span class="ai-chip-lbl">Baixo estoque</span>
          </div>
          <div class="ai-chip" [ngClass]="insight.produtosEsgotados > 0 ? 'ai-chip--danger' : 'ai-chip--ok'">
            <span class="ai-chip-val">{{ insight.produtosEsgotados }}</span>
            <span class="ai-chip-lbl">Esgotados</span>
          </div>
          <div class="ai-chip ai-chip--neutral" style="margin-left:auto">
            <span class="ai-chip-lbl">Gerado em {{ insight.geradoEm | date:'HH:mm:ss' }}</span>
          </div>
        </div>

        <div class="ai-divider"></div>

        <!-- Texto da análise -->
        <div class="ai-analise">{{ insight.analise }}</div>

      </div>

      <!-- Idle state -->
      <div *ngIf="!insight && !isLoading" class="ai-idle">
        Clique em <strong>Analisar Estoque</strong> para gerar uma análise inteligente
        com base nos dados reais do seu inventário.
      </div>

    </div>
  `,
  styles: [`
    .ai-card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      overflow: hidden;
      transition: border-color 0.2s;
    }
    .ai-card--loading { border-color: rgba(30,111,191,0.3); }
    .ai-card--done    { border-color: rgba(30,111,191,0.2); }

    .ai-header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 16px 20px;
      background: linear-gradient(135deg, #0F2D52 0%, #1A4A82 100%);
    }

    .ai-header-left { display: flex; align-items: center; gap: 12px; }

    .ai-icon {
      width: 36px; height: 36px; border-radius: 8px;
      background: rgba(255,255,255,0.12);
      border: 1px solid rgba(255,255,255,0.2);
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }
    .ai-icon svg { width: 18px; height: 18px; color: #fff; }

    .ai-title    { font-size: 13px; font-weight: 500; color: #fff; }
    .ai-subtitle { font-size: 11px; color: rgba(255,255,255,0.5); margin-top: 2px; }

    .ai-btn { background: rgba(255,255,255,0.15) !important; border-color: rgba(255,255,255,0.25) !important; color: #fff !important; }
    .ai-btn:hover:not(:disabled) { background: rgba(255,255,255,0.22) !important; }

    /* Loading */
    .ai-loading { padding: 28px 20px; text-align: center; }
    .ai-loading-dots { display: flex; gap: 6px; justify-content: center; margin-bottom: 12px; }
    .ai-loading-dots span {
      width: 8px; height: 8px; border-radius: 50%;
      background: var(--brand-accent); animation: dotBounce 1.2s ease-in-out infinite;
    }
    .ai-loading-dots span:nth-child(2) { animation-delay: 0.2s; }
    .ai-loading-dots span:nth-child(3) { animation-delay: 0.4s; }
    @keyframes dotBounce {
      0%, 80%, 100% { transform: scale(0.7); opacity: 0.4; }
      40%            { transform: scale(1);   opacity: 1; }
    }
    .ai-loading-text { font-size: 12px; color: var(--text-muted); }

    /* Chips */
    .ai-chips { display: flex; gap: 8px; flex-wrap: wrap; padding: 16px 20px 0; align-items: center; }
    .ai-chip {
      display: flex; align-items: center; gap: 6px;
      padding: 5px 10px; border-radius: 20px; font-size: 11px;
    }
    .ai-chip-val { font-family: var(--mono); font-weight: 500; font-size: 13px; }
    .ai-chip-lbl { color: inherit; opacity: 0.75; }

    .ai-chip--neutral { background: var(--surface-2); color: var(--text-secondary); }
    .ai-chip--ok      { background: var(--success-bg); color: var(--success); }
    .ai-chip--warn    { background: var(--warning-bg); color: var(--warning); }
    .ai-chip--danger  { background: var(--danger-bg);  color: var(--danger); }

    .ai-divider { height: 1px; background: var(--border); margin: 14px 20px 0; }

    /* Analysis text */
    .ai-analise {
      padding: 14px 20px 20px;
      font-size: 12.5px; color: var(--text-secondary);
      line-height: 1.75; white-space: pre-wrap;
    }

    /* Idle */
    .ai-idle {
      padding: 20px;
      font-size: 12px; color: var(--text-muted);
      text-align: center; line-height: 1.6;
    }
    .ai-idle strong { color: var(--text-secondary); }
  `]
})
export class AiInsightsComponent implements OnDestroy {
  insight: InsightResponse | null = null;
  isLoading = false;
  private subs = new Subscription();

  constructor(private insightsService: InsightsService) {}

  analisar(): void {
    if (this.isLoading) return;
    this.isLoading = true;
    this.insight   = null;

    this.subs.add(
      this.insightsService.analisarEstoque()
        .pipe(finalize(() => (this.isLoading = false)))
        .subscribe({
          next:  r   => (this.insight = r),
          error: err => console.error('Erro ao analisar estoque:', err)
        })
    );
  }

  ngOnDestroy(): void { this.subs.unsubscribe(); }
}
