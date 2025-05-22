import { Injectable } from '@angular/core';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class TokenStorageService {
  private readonly TOKEN_KEY = 'access_token';
  private readonly EXPIRATION_KEY = 'token_expiration';
  private readonly AUTH_STATE_KEY = 'is_authenticated';
  private readonly REMEMBER_ME_KEY = 'remember_me';

  constructor(private router: Router) {}

  public getToken(): string | null {
    const rememberMe = localStorage.getItem(this.REMEMBER_ME_KEY) === 'true';

    if (rememberMe) {
      return localStorage.getItem(this.TOKEN_KEY);
    } else {
      return sessionStorage.getItem(this.TOKEN_KEY);
    }
  }

  public saveToken(token: string, rememberMe: boolean): void {
    localStorage.setItem(this.REMEMBER_ME_KEY, rememberMe.toString());

    const storage = rememberMe ? localStorage : sessionStorage;

    storage.setItem(this.TOKEN_KEY, token);

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      if (payload.exp) {
        const expiration = new Date(payload.exp * 1000);
        storage.setItem(this.EXPIRATION_KEY, expiration.toISOString());
      }
    } catch (e) {
      console.error('TokenStorageService: Failed to parse token expiration', e);
    }

    this.setAuthenticated(true);
  }

  public hasValidToken(): boolean {
    const token = this.getToken();
    if (!token) {
      return false;
    }

    const rememberMe = localStorage.getItem(this.REMEMBER_ME_KEY) === 'true';
    const storage = rememberMe ? localStorage : sessionStorage;

    const expiration = storage.getItem(this.EXPIRATION_KEY);
    if (expiration) {
      const expirationDate = new Date(expiration);
      const now = new Date();

      if (now > expirationDate) {
        return false;
      }
    }

    return true;
  }

  public setAuthenticated(state: boolean): void {
    const rememberMe = localStorage.getItem(this.REMEMBER_ME_KEY) === 'true';
    const storage = rememberMe ? localStorage : sessionStorage;

    storage.setItem(this.AUTH_STATE_KEY, state.toString());
  }

  public isAuthenticated(): boolean {
    const rememberMe = localStorage.getItem(this.REMEMBER_ME_KEY) === 'true';
    const storage = rememberMe ? localStorage : sessionStorage;

    return storage.getItem(this.AUTH_STATE_KEY) === 'true';
  }

  public clearTokens(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.EXPIRATION_KEY);
    localStorage.removeItem(this.AUTH_STATE_KEY);
    localStorage.removeItem(this.REMEMBER_ME_KEY);

    sessionStorage.removeItem(this.TOKEN_KEY);
    sessionStorage.removeItem(this.EXPIRATION_KEY);
    sessionStorage.removeItem(this.AUTH_STATE_KEY);
  }

  public redirectToLogin(): void {
    this.router.navigate(['/login']);
  }
}
