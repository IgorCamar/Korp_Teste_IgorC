import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { NotaFiscalService, CriarNotaRequest } from '../services/nota-fiscal.service';
import { ProdutoService, Produto } from '../../inventory/services/produto.service';

@Component({
  selector: 'app-create-invoice',
  templateUrl: './create-invoice.component.html'
})
export class CreateInvoiceComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  produtos: Produto[] = [];
  isSaving = false;
  errorMessage: string | null = null;
  private subs = new Subscription();

  constructor(
    private fb: FormBuilder,
    private notaService: NotaFiscalService,
    private produtoService: ProdutoService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({ itens: this.fb.array([this.criarItem()]) });
    const s = this.produtoService.listar().subscribe({ next: p => (this.produtos = p) });
    this.subs.add(s);
  }

  ngOnDestroy(): void { this.subs.unsubscribe(); }

  get itens(): FormArray { return this.form.get('itens') as FormArray; }

  criarItem(): FormGroup {
    return this.fb.group({
      codigoProduto:    ['', Validators.required],
      descricaoProduto: ['', Validators.required],
      quantidade:       [1,  [Validators.required, Validators.min(0.01)]],
      valorUnitario:    [0,  [Validators.required, Validators.min(0.01)]]
    });
  }

  adicionarItem(): void { this.itens.push(this.criarItem()); }
  removerItem(i: number): void { if (this.itens.length > 1) this.itens.removeAt(i); }

  onProdutoSelecionado(i: number): void {
    const ctrl = this.itens.at(i);
    const p = this.produtos.find(x => x.codigo === ctrl.get('codigoProduto')?.value);
    if (p) ctrl.get('descricaoProduto')?.setValue(p.descricao);
  }

  subtotal(i: number): number {
    const v = this.itens.at(i).value;
    return (v.quantidade || 0) * (v.valorUnitario || 0);
  }

  get total(): number {
    return this.itens.controls.reduce((acc, c) => {
      const v = c.value;
      return acc + (v.quantidade || 0) * (v.valorUnitario || 0);
    }, 0);
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.isSaving = true;
    this.errorMessage = null;
    const payload: CriarNotaRequest = { itens: this.itens.value };
    const s = this.notaService.criar(payload)
      .pipe(finalize(() => (this.isSaving = false)))
      .subscribe({
        next: () => this.router.navigate(['/invoices']),
        error: (err: any) => (this.errorMessage = err?.error?.message ?? 'Erro ao criar nota fiscal.')
      });
    this.subs.add(s);
  }

  voltar(): void { this.router.navigate(['/invoices']); }
}
