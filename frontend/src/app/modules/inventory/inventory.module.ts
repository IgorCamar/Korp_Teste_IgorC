import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';

import { ProductListComponent } from './components/product-list.component';
import { AiInsightsComponent } from './components/ai-insights.component';

const routes: Routes = [{ path: '', component: ProductListComponent }];

@NgModule({
  declarations: [ProductListComponent, AiInsightsComponent],
  imports: [CommonModule, ReactiveFormsModule, SharedModule, RouterModule.forChild(routes)]
})
export class InventoryModule {}
