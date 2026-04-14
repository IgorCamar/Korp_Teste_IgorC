import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-confirm-dialog',
  template: `
    <div class="modal-backdrop" (click)="onCancel()">
      <div class="modal-box confirm-box" (click)="$event.stopPropagation()">

        <div class="confirm-icon" [ngClass]="type">
          <svg *ngIf="type === 'danger'" viewBox="0 0 24 24">
            <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/>
            <line x1="12" y1="9" x2="12" y2="13"/>
            <line x1="12" y1="17" x2="12.01" y2="17"/>
          </svg>
          <svg *ngIf="type === 'warning'" viewBox="0 0 24 24">
            <circle cx="12" cy="12" r="10"/>
            <line x1="12" y1="8" x2="12" y2="12"/>
            <line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
        </div>

        <div class="confirm-title">{{ title }}</div>
        <div class="confirm-message">{{ message }}</div>

        <div *ngIf="detail" class="confirm-detail">{{ detail }}</div>

        <div class="confirm-actions">
          <button class="btn btn-ghost" (click)="onCancel()">{{ cancelLabel }}</button>
          <button class="btn" [ngClass]="'btn-confirm-' + type" (click)="onConfirm()">
            {{ confirmLabel }}
          </button>
        </div>

      </div>
    </div>
  `,
  styles: [`
    .confirm-box { max-width: 400px; text-align: center; padding: 0; }

    .confirm-icon {
      display: flex; align-items: center; justify-content: center;
      width: 52px; height: 52px; border-radius: 50%;
      margin: 28px auto 16px;
    }
    .confirm-icon svg { width: 24px; height: 24px; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
    .confirm-icon.danger  { background: #FEF2F2; }
    .confirm-icon.danger svg { stroke: #B91C1C; }
    .confirm-icon.warning { background: #FFFBEB; }
    .confirm-icon.warning svg { stroke: #92400E; }

    .confirm-title { font-size: 15px; font-weight: 500; color: var(--text-primary); padding: 0 28px; }
    .confirm-message { font-size: 13px; color: var(--text-secondary); padding: 8px 28px 0; line-height: 1.6; }

    .confirm-detail {
      margin: 12px 24px 0; padding: 10px 14px;
      background: var(--surface-2); border-radius: var(--radius);
      font-size: 12px; color: var(--text-muted);
      font-family: var(--mono); text-align: left;
    }

    .confirm-actions { display: flex; gap: 8px; justify-content: center; padding: 20px 24px 24px; }

    .btn-confirm-danger  { background: #B91C1C; color: #fff; border-color: #B91C1C; }
    .btn-confirm-danger:hover  { background: #991B1B !important; border-color: #991B1B !important; }
    .btn-confirm-warning { background: #92400E; color: #fff; border-color: #92400E; }
    .btn-confirm-warning:hover { background: #78350F !important; border-color: #78350F !important; }
  `]
})
export class ConfirmDialogComponent {
  @Input() title         = 'Confirmar ação';
  @Input() message       = 'Tem certeza?';
  @Input() detail        = '';
  @Input() confirmLabel  = 'Confirmar';
  @Input() cancelLabel   = 'Cancelar';
  @Input() type: 'danger' | 'warning' = 'danger';

  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  onConfirm(): void { this.confirmed.emit(); }
  onCancel(): void  { this.cancelled.emit(); }
}
