import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';

import { InvoiceListComponent } from './components/invoice-list.component';
import { CreateInvoiceComponent } from './components/create-invoice.component';

const routes: Routes = [
  { path: '',    component: InvoiceListComponent },
  { path: 'new', component: CreateInvoiceComponent }
];

@NgModule({
  declarations: [InvoiceListComponent, CreateInvoiceComponent],
  imports: [CommonModule, ReactiveFormsModule, SharedModule, RouterModule.forChild(routes)]
})
export class InvoiceModule {}
