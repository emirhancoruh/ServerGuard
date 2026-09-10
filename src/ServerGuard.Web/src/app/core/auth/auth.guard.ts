import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

const LOGIN_PATH = '/login';

/**
 * Oturum açılmamışsa giriş ekranına yönlendirir.
 * Gidilmek istenen adres <c>returnUrl</c> ile taşınır; giriş sonrası oraya dönülür.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree([LOGIN_PATH], { queryParams: { returnUrl: state.url } });
};

/**
 * Zaten oturum açmış bir kullanıcının giriş ekranını görmesini engeller.
 */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? router.createUrlTree(['/']) : true;
};
