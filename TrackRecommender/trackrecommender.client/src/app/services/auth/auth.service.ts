import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, throwError } from 'rxjs';
import { catchError, tap, switchMap } from 'rxjs/operators';
import {
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  UserProfile,
} from '../../models/auth.models';
import { TokenStorageService } from '../token-storage/token-storage.service';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private currentUserSubject = new BehaviorSubject<UserProfile | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private http: HttpClient,
    private tokenStorage: TokenStorageService,
    private router: Router,
  ) {
    this.checkAuthState();
  }

  private checkAuthState(): void {
    if (this.tokenStorage.hasValidToken()) {
      this.validateToken();
    }
  }

  private validateToken(): void {
    this.getUserProfile().subscribe({
      next: (profile) => {
        this.tokenStorage.setAuthenticated(true);
        this.currentUserSubject.next(profile);
      },
      error: (error) => {
        if (error instanceof HttpErrorResponse && error.status === 401) {
          this.refreshToken().subscribe({
            next: () => {
              this.getUserProfile().subscribe({
                next: (profile) => {
                  this.tokenStorage.setAuthenticated(true);
                  this.currentUserSubject.next(profile);
                },
                error: () => {
                  this.tokenStorage.setAuthenticated(false);

                  if (
                    !window.location.pathname.includes('/login') &&
                    !window.location.pathname.includes('/register')
                  ) {
                    this.router.navigate(['/login']);
                  }
                },
              });
            },
            error: () => {
              this.tokenStorage.setAuthenticated(false);

              if (
                !window.location.pathname.includes('/login') &&
                !window.location.pathname.includes('/register')
              ) {
                this.router.navigate(['/login']);
              }
            },
          });
        } else {
          this.tokenStorage.setAuthenticated(false);

          if (
            !window.location.pathname.includes('/login') &&
            !window.location.pathname.includes('/register')
          ) {
            this.router.navigate(['/login']);
          }
        }
      },
    });
  }

  public login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`/api/auth/login`, credentials, {
        withCredentials: true,
      })
      .pipe(
        tap((response) => {
          const rememberMe = credentials.rememberMe || false;
          this.tokenStorage.saveToken(response.accessToken, rememberMe);
          this.getUserProfile().subscribe();
        }),
        catchError(this.handleError),
      );
  }

  public register(userData: RegisterRequest): Observable<any> {
    return this.http
      .post(`/api/auth/register`, userData)
      .pipe(catchError(this.handleError));
  }

  public logout(): Observable<any> {
    return this.http
      .post(`/api/auth/logout`, {}, { withCredentials: true })
      .pipe(
        tap(() => {
          this.clearUserSession();
        }),
        catchError((error) => {
          this.clearUserSession();
          return throwError(() => this.handleError(error));
        }),
      );
  }

  private clearUserSession(): void {
    this.currentUserSubject.next(null);
    this.tokenStorage.clearTokens();
    this.tokenStorage.redirectToLogin();
  }

  public refreshToken(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(
        `/api/auth/refresh-token`,
        {},
        { withCredentials: true },
      )
      .pipe(
        tap((response) => {
          const rememberMe = localStorage.getItem('remember_me') === 'true';
          this.tokenStorage.saveToken(response.accessToken, rememberMe);
        }),
        catchError((error) => {
          return throwError(() => this.handleError(error));
        }),
      );
  }

  public getUserProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`/api/user/profile`).pipe(
      tap((profile) => {
        this.currentUserSubject.next(profile);
      }),
      catchError((error: HttpErrorResponse) => {
        return throwError(() => this.handleError(error));
      }),
    );
  }

  private handleError(error: HttpErrorResponse) {
    let errorMessage = 'An unexpected error occurred';

    if (error.error instanceof ErrorEvent) {
      errorMessage = `Error: ${error.error.message}`;
    } else {
      if (error.error && error.error.message) {
        errorMessage = error.error.message;
      } else if (error.status === 401) {
        errorMessage = 'Incorrect username or password';
      } else if (error.status === 400) {
        errorMessage = 'Invalid input data';
      }
    }

    return throwError(() => errorMessage);
  }
}
