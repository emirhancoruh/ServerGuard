import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../core/services/auth.service';

const DEFAULT_RETURN_URL = '/';
const UNAUTHORIZED = 401;

@Component({
  selector: 'sg-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  private readonly submittingSignal = signal(false);
  private readonly errorSignal = signal<string | null>(null);

  readonly submitting = this.submittingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  userName = '';
  password = '';

  submit(): void {
    if (this.submitting() || this.userName.length === 0 || this.password.length === 0) {
      return;
    }

    this.submittingSignal.set(true);
    this.errorSignal.set(null);

    this.auth
      .login(this.userName, this.password)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submittingSignal.set(false);
          void this.router.navigateByUrl(this.resolveReturnUrl());
        },
        error: (error: unknown) => {
          this.submittingSignal.set(false);
          this.errorSignal.set(describe(error));
          this.password = '';
        }
      });
  }

  /**
   * Giriş öncesinde gidilmek istenen adrese döner. Dışarıdan gelen bir değer olduğundan
   * yalnızca uygulama içi göreli yollar kabul edilir; aksi halde bu parametre kullanıcıyı
   * başka bir siteye yönlendirmek için kullanılabilirdi.
   */
  private resolveReturnUrl(): string {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl');

    if (requested === null || !requested.startsWith('/') || requested.startsWith('//')) {
      return DEFAULT_RETURN_URL;
    }

    return requested;
  }
}

/** Kullanıcıya gösterilecek hata metni. Sunucunun döndüğü açıklama varsa o kullanılır. */
function describe(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Beklenmeyen bir hata oluştu.';
  }

  if (error.status === 0) {
    return 'Sunucuya ulaşılamıyor. API çalışıyor mu?';
  }

  const detail = (error.error as { detail?: unknown } | null)?.detail;

  if (typeof detail === 'string' && detail.length > 0) {
    return detail;
  }

  return error.status === UNAUTHORIZED
    ? 'Kullanıcı adı veya parola hatalı.'
    : `Giriş yapılamadı (HTTP ${error.status}).`;
}
