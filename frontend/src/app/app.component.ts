import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  template: `
    <div class="shell">
      <aside class="sidebar">
        <div class="sidebar-logo">
          <div class="logo-mark">
            <div class="logo-square">
              <svg viewBox="0 0 13 13">
                <rect x="1" y="1" width="4.5" height="4.5" rx="1"/>
                <rect x="7.5" y="1" width="4.5" height="4.5" rx="1"/>
                <rect x="1" y="7.5" width="4.5" height="4.5" rx="1"/>
                <rect x="7.5" y="7.5" width="4.5" height="4.5" rx="1"/>
              </svg>
            </div>
            <div>
              <div class="logo-name">Korp ERP</div>
              <div class="logo-sub">Enterprise</div>
            </div>
          </div>
        </div>

        <div class="nav-section">
          <div class="nav-label">Operações</div>
          <a class="nav-item" routerLink="/products" routerLinkActive="active">
            <svg viewBox="0 0 13 13"><path d="M6.5 1L12 4v3c0 3-2.5 5-5.5 5.5C3.5 12 1 10 1 7V4z"/></svg>
            Estoque
          </a>
          <a class="nav-item" routerLink="/invoices" routerLinkActive="active">
            <svg viewBox="0 0 13 13"><rect x="1" y="2" width="11" height="9" rx="1"/><path d="M4 2V1M9 2V1M1 6h11"/></svg>
            Notas Fiscais
          </a>
          <a class="nav-item" routerLink="/invoices/new" routerLinkActive="active">
            <svg viewBox="0 0 13 13"><path d="M6.5 1v11M1 6.5h11"/></svg>
            Nova Nota
          </a>
        </div>

        <div class="nav-section">
          <div class="nav-label">Sistema</div>
          <a class="nav-item">
            <svg viewBox="0 0 13 13"><circle cx="6.5" cy="6.5" r="2"/><path d="M6.5 1v1.5M6.5 10.5V12M1 6.5h1.5M10.5 6.5H12M2.8 2.8l1 1M9.2 9.2l1 1M2.8 10.2l1-1M9.2 3.8l1-1"/></svg>
            Configurações
          </a>
        </div>

        <div class="sidebar-footer">
          <div class="user-row">
            <div class="user-avatar">IC</div>
            <div>
              <div class="user-name">Igor Camar</div>
              <div class="user-role">Administrador</div>
            </div>
          </div>
        </div>
      </aside>

      <div class="main">
        <router-outlet></router-outlet>
      </div>
    </div>
  `
})
export class AppComponent {}
