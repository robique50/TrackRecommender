import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { Observable, of } from 'rxjs';
import { TokenStorageService } from '../services/token-storage/token-storage.service';

@Injectable({
  providedIn: 'root',
})
export class AuthGuard implements CanActivate {
  constructor(
    private tokenService: TokenStorageService,
    private router: Router,
  ) {}

  canActivate(): Observable<boolean | UrlTree> {
    const isAuthenticated = this.tokenService.isAuthenticated();

    if (isAuthenticated) {
      return of(true);
    }

    return of(this.router.createUrlTree(['/login']));
  }
}
