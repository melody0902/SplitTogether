import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom, catchError, throwError } from 'rxjs';
import { User } from './models';
@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  user = signal<User | null>(null);
  initialized = false;
  get<T>(url: string) {
    return firstValueFrom(this.http.get<T>('/api' + url));
  }
  post<T>(url: string, body: unknown) {
    return firstValueFrom(this.http.post<T>('/api' + url, body));
  }
  put<T>(url: string, body: unknown) {
    return firstValueFrom(this.http.put<T>('/api' + url, body));
  }
  patch<T>(url: string, body: unknown) {
    return firstValueFrom(this.http.patch<T>('/api' + url, body));
  }
  delete<T>(url: string) {
    return firstValueFrom(this.http.delete<T>('/api' + url));
  }
  async init() {
    if (!this.initialized) {
      try {
        this.user.set(await this.get<User>('/auth/me'));
      } catch (e) {
        if (!(e instanceof HttpErrorResponse) || e.status !== 401) throw e;
      }
      await this.csrf();
      this.initialized = true;
    }
  }
  csrf() {
    return this.get('/auth/csrf');
  }
  async logout() {
    await this.post('/auth/logout', {});
    this.user.set(null);
    this.initialized = false;
    location.assign('/login');
  }
}
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const api = inject(Api);
  const router = inject(Router);
  return next(req).pipe(
    catchError((e: HttpErrorResponse) => {
      if (e.status === 401 && !req.url.endsWith('/auth/me')) {
        api.user.set(null);
        api.initialized = false;
        void router.navigate(['/login']);
      }
      return throwError(() => e);
    }),
  );
};
export function message(e: unknown): string {
  if (e instanceof HttpErrorResponse)
    return e.status === 0
      ? '無法連線至服務，請檢查後端是否已啟動'
      : (e.error?.title ?? '操作失敗，請稍後再試');
  return e instanceof Error ? e.message : '操作失敗';
}
