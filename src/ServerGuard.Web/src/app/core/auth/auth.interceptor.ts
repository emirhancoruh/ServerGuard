import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { ApiRoutes } from '../api-routes';
import { AuthService } from '../services/auth.service';

const UNAUTHORIZED = 401;
const LOGIN_PATH = '/login';

/**
 * Her isteğe oturum token'ını ekler ve token geçersizleştiğinde kullanıcıyı giriş ekranına alır.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Giriş isteğinin kendisine token eklenmez; ayrıca 401 yanıtı orada normaldir
  // (hatalı parola) ve yönlendirme tetiklememelidir.
  const isLoginRequest = request.url.endsWith(ApiRoutes.login);
  const token = auth.token();

  const authorized = token !== null && !isLoginRequest
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (!isLoginRequest && error instanceof HttpErrorResponse && error.status === UNAUTHORIZED) {
        // Token süresi dolmuş veya iptal edilmiş olabilir; oturumu temizleyip yeniden giriş istenir.
        auth.logout();
        void router.navigate([LOGIN_PATH], { queryParams: { returnUrl: router.url } });
      }

      return throwError(() => error);
    })
  );
};
