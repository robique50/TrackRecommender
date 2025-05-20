import { HttpRequest, HttpHandlerFn, HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TokenStorageService } from '../services/token-storage/token-storage.service';
import { inject } from '@angular/core';
import { Router } from '@angular/router';

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<any> => {
  const tokenStorage = inject(TokenStorageService);
  const router = inject(Router);

  if (req.url.includes('/api/auth/login') ||
    req.url.includes('/api/auth/register') ||
    req.url.includes('/api/auth/refresh-token')) {
    return next(req);
  }

  const token = tokenStorage.getToken();

  if (token) {
    const authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });

    return next(authReq).pipe(
      catchError((error) => {
        if (error instanceof HttpErrorResponse && error.status === 401) {
          tokenStorage.setAuthenticated(false);

          if (!window.location.pathname.includes('/login') &&
            !window.location.pathname.includes('/register')) {
            router.navigate(['/login']);
          }
        }

        return throwError(() => error);
      })
    );
  }

  return next(req);
};
