import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { ProdutoService, Produto } from '../services/produto.service';

interface Toast { title: string; message: string; type: 'success' | 'error' | 'warning'; }

// Modal modes
type ModalMode = 'none' | 'criar' | 'editar' | 'incrementar';

@Component({
  selector: 'app-product-list',
  templateUrl: './product-list.component.html'
})
export class ProductListComponent implements OnInit, OnDestroy {
  produtos: Produto[] = [];
  filtered: Produto[] = [];
  isLoading = false;
  isSaving = false;
  searchTerm = '';
  toast: Toast | null = null;
  private toastTimer: any;

  confirmDialog: {
    visible: boolean; title: string; message: string;
    detail: string; type: 'danger' | 'warning'; confirmLabel: string; action: () => void;
  } = { visible: false, title: '', message: '', detail: '', type: 'danger', confirmLabel: 'Confirmar', action: () => {} };
  private subs = new Subscription();

  // Modal state
  modalMode: ModalMode = 'none';
  selectedProduto: Produto | null = null;
  form!: FormGroup;
  incrementForm!: FormGroup;

  constructor(private service: ProdutoService, private fb: FormBuilder) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      codigo:       ['', [Validators.required, Validators.maxLength(50)]],
      descricao:    ['', [Validators.required, Validators.maxLength(200)]],
      saldoInicial: [0,  [Validators.required, Validators.min(0)]]
    });
    this.incrementForm = this.fb.group({
      quantidade: [1, [Validators.required, Validators.min(0.01)]]
    });
    this.carregar();
  }

  ngOnDestroy(): void { this.subs.unsubscribe(); }

  carregar(): void {
    this.isLoading = true;
    this.subs.add(
      this.service.listar()
        .pipe(finalize(() => (this.isLoading = false)))
        .subscribe({
          next: p => { this.produtos = p; this.applyFilter(); },
          error: () => this.notify('Erro', 'Não foi possível carregar os produtos.', 'error')
        })
    );
  }

  onSearch(term: string): void { this.searchTerm = term; this.applyFilter(); }

  applyFilter(): void {
    const t = this.searchTerm.toLowerCase().trim();
    this.filtered = t
      ? this.produtos.filter(p => p.codigo.toLowerCase().includes(t) || p.descricao.toLowerCase().includes(t))
      : [...this.produtos];
  }

  // ── Modal de criação ──────────────────────────────
  abrirCriar(): void {
    this.modalMode = 'criar';
    this.form.reset({ saldoInicial: 0 });
    this.form.get('codigo')?.enable();
  }

  // ── Modal de edição ───────────────────────────────
  abrirEditar(p: Produto): void {
    this.selectedProduto = p;
    this.modalMode = 'editar';
    this.form.patchValue({ codigo: p.codigo, descricao: p.descricao, saldoInicial: p.saldo });
    this.form.get('codigo')?.disable();
  }

  // ── Modal de incremento ───────────────────────────
  abrirIncrementar(p: Produto): void {
    this.selectedProduto = p;
    this.modalMode = 'incrementar';
    this.incrementForm.reset({ quantidade: 1 });
  }

  fecharModal(): void { this.modalMode = 'none'; this.selectedProduto = null; }

  onSubmit(): void {
    if (this.modalMode === 'criar')    this.criar();
    if (this.modalMode === 'editar')   this.editar();
  }

  private criar(): void {
    if (this.form.invalid) return;
    this.isSaving = true;
    this.subs.add(
      this.service.criar(this.form.value)
        .pipe(finalize(() => (this.isSaving = false)))
        .subscribe({
          next: p => {
            this.produtos = [p, ...this.produtos];
            this.applyFilter();
            this.fecharModal();
            this.notify('Produto criado', `${p.codigo} adicionado ao estoque.`, 'success');
          },
          error: (err: any) => this.notify('Erro', err?.error?.message ?? 'Erro ao criar produto.', 'error')
        })
    );
  }

  private editar(): void {
    if (this.form.invalid || !this.selectedProduto) return;
    this.isSaving = true;
    this.subs.add(
      this.service.atualizar(this.selectedProduto.id, { descricao: this.form.get('descricao')?.value })
        .pipe(finalize(() => (this.isSaving = false)))
        .subscribe({
          next: updated => {
            this.produtos = this.produtos.map(p => p.id === updated.id ? updated : p);
            this.applyFilter();
            this.fecharModal();
            this.notify('Produto atualizado', `${updated.codigo} atualizado.`, 'success');
          },
          error: (err: any) => this.notify('Erro', err?.error?.message ?? 'Erro ao atualizar.', 'error')
        })
    );
  }

  onIncrementar(): void {
    if (this.incrementForm.invalid || !this.selectedProduto) return;
    this.isSaving = true;
    this.subs.add(
      this.service.incrementarSaldo(this.selectedProduto.id, this.incrementForm.value.quantidade)
        .pipe(finalize(() => (this.isSaving = false)))
        .subscribe({
          next: updated => {
            this.produtos = this.produtos.map(p => p.id === updated.id ? updated : p);
            this.applyFilter();
            this.fecharModal();
            this.notify('Saldo incrementado', `${updated.codigo}: novo saldo ${updated.saldo}.`, 'success');
          },
          error: (err: any) => this.notify('Erro', err?.error?.message ?? 'Erro ao incrementar.', 'error')
        })
    );
  }

  deletar(p: Produto): void {
    this.confirmDialog = {
      visible: true,
      title: 'Excluir Produto',
      message: `Deseja excluir o produto ${p.codigo} permanentemente?`,
      detail: `${p.descricao} · Saldo atual: ${p.saldo}`,
      type: 'danger',
      confirmLabel: 'Excluir',
      action: () => this.executarDelete(p)
    };
  }

  onConfirmDialogConfirm(): void { this.confirmDialog.action(); this.confirmDialog.visible = false; }
  onConfirmDialogCancel(): void  { this.confirmDialog.visible = false; }

  private executarDelete(p: Produto): void {
    this.subs.add(
      this.service.deletar(p.id).subscribe({
        next: () => {
          this.produtos = this.produtos.filter(x => x.id !== p.id);
          this.applyFilter();
          this.notify('Produto removido', `${p.codigo} excluído.`, 'success');
        },
        error: () => this.notify('Erro', 'Não foi possível excluir o produto.', 'error')
      })
    );
  }

  // ── Helpers ───────────────────────────────────────
  stockClass(p: Produto): string {
    if (p.saldo === 0) return 's-zero';
    if (p.saldo < 10)  return 's-low';
    return 's-ok';
  }

  stockWidth(p: Produto): string {
    const max = Math.max(...this.produtos.map(x => x.saldo), 1);
    return Math.round((p.saldo / max) * 100) + '%';
  }

  stockBadge(p: Produto): { label: string; cls: string } {
    if (p.saldo === 0) return { label: 'Esgotado', cls: 'badge-alert' };
    if (p.saldo < 10)  return { label: 'Baixo',    cls: 'badge-warning' };
    return { label: 'Normal', cls: 'badge-open' };
  }

  get totalProdutos(): number { return this.produtos.length; }
  get totalBaixo(): number    { return this.produtos.filter(p => p.saldo > 0 && p.saldo < 10).length; }
  get totalEsgotado(): number { return this.produtos.filter(p => p.saldo === 0).length; }
  get saldoTotal(): number    { return this.produtos.reduce((a, p) => a + p.saldo, 0); }

  private notify(title: string, message: string, type: 'success' | 'error' | 'warning'): void {
    clearTimeout(this.toastTimer);
    this.toast = { title, message, type };
    this.toastTimer = setTimeout(() => (this.toast = null), 5000);
  }

  dismissToast(): void { this.toast = null; }
}
