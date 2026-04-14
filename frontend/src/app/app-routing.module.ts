import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'invoices', pathMatch: 'full' },
  {
    path: 'products',
    loadChildren: () =>
      import('./modules/inventory/inventory.module').then(m => m.InventoryModule)
  },
  {
    path: 'invoices',
    loadChildren: () =>
      import('./modules/invoice/invoice.module').then(m => m.InvoiceModule)
  },
  { path: '**', redirectTo: 'invoices' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}
