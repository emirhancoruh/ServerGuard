import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiRoutes } from '../api-routes';
import { AuthSession, isSessionValid, toAuthSession } from '../models/auth-session';

/**
 * Panel oturumunu yönetir: giriş, çıkış ve token'ın saklanması.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  /**
   * Oturumun saklandığı anahtar. localStorage kullanılır; böylece sayfa yenilendiğinde
   * veya sekme kapatılıp açıldığında yeniden giriş istenmez. Token kısa ömürlüdür
   * (varsayılan 8 saat) ve süresi dolduğunda otomatik olarak atılır.
   */
  private static readonly storageKey = 'serverguard.session';

  private readonly http = inject(HttpClient);
  private readonly sessionSignal = signal<AuthSession | null>(AuthService.restore());

  /** Oturum açılmış mı? Yönlendirme koruması ve menü görünürlüğü buna bakar. */
  readonly isAuthenticated = computed(() => isSessionValid(this.sessionSignal()));

  readonly userName = computed(() => this.sessionSignal()?.userName ?? '');

  /**
   * Geçerli erişim token'ı; oturum yoksa veya süresi dolmuşsa <c>null</c>.
   * Süresi dolmuş token'ı göndermek yerine hiç göndermemek, sunucuda gereksiz
   * doğrulama ve log gürültüsü oluşmasını önler.
   */
  token(): string | null {
    const session = this.sessionSignal();

    if (!isSessionValid(session)) {
      return null;
    }

    return session!.accessToken;
  }

  login(userName: string, password: string): Observable<void> {
    return this.http
      .post<unknown>(`${environment.apiBaseUrl}${ApiRoutes.login}`, { userName, password })
      .pipe(
        map((raw) => {
          const session = toAuthSession(raw);

          if (session === null) {
            throw new Error('Sunucudan beklenmeyen bir oturum yanıtı geldi.');
          }

          return session;
        }),
        tap((session) => this.store(session)),
        map(() => undefined)
      );
  }

  /** Oturumu kapatır. Token sunucuda saklanmadığından yalnızca yerelden silinir. */
  logout(): void {
    this.sessionSignal.set(null);

    try {
      localStorage.removeItem(AuthService.storageKey);
    } catch {
      // Tarayıcı depolamayı engelliyorsa sinyalin temizlenmesi yeterlidir.
    }
  }

  private store(session: AuthSession): void {
    this.sessionSignal.set(session);

    try {
      localStorage.setItem(AuthService.storageKey, JSON.stringify(session));
    } catch {
      // Depolama kullanılamıyorsa oturum yalnızca bu sayfa ömrü boyunca sürer.
    }
  }

  /**
   * Sayfa yenilendiğinde önceki oturumu geri yükler. Bozuk veya süresi dolmuş kayıt
   * temizlenir; kullanıcı giriş ekranına düşer.
   */
  private static restore(): AuthSession | null {
    try {
      const raw = localStorage.getItem(AuthService.storageKey);

      if (raw === null) {
        return null;
      }

      const session = toAuthSession(JSON.parse(raw));

      if (!isSessionValid(session)) {
        localStorage.removeItem(AuthService.storageKey);
        return null;
      }

      return session;
    } catch {
      return null;
    }
  }
}
