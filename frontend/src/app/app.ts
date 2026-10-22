import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { Api } from './api';
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, MatButtonModule],
  template: `<header class="topbar">
      <a class="brand" routerLink="/groups"><span class="brand-icon">S</span> SplitTogether</a>
      <div class="account">
        @if (api.user(); as user) {
          <span>{{ user.name }}</span
          ><button mat-button (click)="logout()">登出</button>
        } @else {
          <span>一起花費，輕鬆分帳</span>
        }
      </div>
    </header>
    <main><router-outlet /></main>
    <footer>SplitTogether · 團體帳務，一目了然</footer>`,
})
export class App {
  api = inject(Api);
  async logout() {
    try {
      await this.api.logout();
    } catch {
      alert('登出失敗，請稍後重試');
    }
  }
}
