import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth/auth.service';
import { TokenStorageService } from '../../services/token-storage/token-storage.service';
import { AuthNavbarComponent } from '../auth-navbar/auth-navbar.component';
import { trigger, transition, style, animate } from '@angular/animations';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    AuthNavbarComponent,
  ],
  animations: [
    trigger('slideIn', [
      transition(':enter', [
        style({ transform: 'translateX(100%)', opacity: 0 }),
        animate(
          '300ms ease-out',
          style({ transform: 'translateX(0)', opacity: 1 })
        ),
      ]),
    ]),
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('300ms ease-out', style({ opacity: 1 })),
      ]),
    ]),
  ],
})
export class LoginComponent implements OnInit {
  public loginForm: FormGroup;
  public isLoading = false;
  public error: string | null = null;
  public showSuccess = false;
  public successMessage = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private tokenStorage: TokenStorageService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      username: ['', [Validators.required]],
      password: ['', [Validators.required]],
      rememberMe: [false],
    });
  }

  ngOnInit(): void {
    if (this.tokenStorage.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    }
  }

  protected onSubmit(): void {
    if (this.loginForm.invalid) {
      Object.keys(this.loginForm.controls).forEach((key) => {
        this.loginForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.isLoading = true;
    this.error = null;

    this.authService.login(this.loginForm.value).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.router.navigate(['/']);
      },
      error: (err: any) => {
        this.isLoading = false;
        let displayMessage = 'Login failed. Please check your credentials.'; // Mesaj implicit specific pentru login

        if (typeof err === 'string' && err.trim() !== '') {
          displayMessage = err;
        } else if (err && err.message && typeof err.message === 'string') {
          if (err.status === 401 || err.status === 400) {
          }
          displayMessage = err.message.includes('Http failure response')
            ? displayMessage
            : err.message;
        } else if (err && err.error && typeof err.error === 'string') {
          displayMessage = err.error;
        } else if (
          err &&
          err.error &&
          err.error.message &&
          typeof err.error.message === 'string'
        ) {
          displayMessage = err.error.message;
        } else if (
          err &&
          err.error &&
          err.error.errors &&
          typeof err.error.errors === 'object'
        ) {
          const errorMessages: string[] = [];
          for (const key in err.error.errors) {
            if (
              err.error.errors.hasOwnProperty(key) &&
              Array.isArray(err.error.errors[key])
            ) {
              errorMessages.push(...err.error.errors[key]);
            }
          }
          if (errorMessages.length > 0) {
            displayMessage = errorMessages.join(' ');
          } else if (err.error.title && typeof err.error.title === 'string') {
            displayMessage = err.error.title;
          }
        }

        this.error = displayMessage;

        setTimeout(() => {
          this.error = null;
        }, 7000);
      },
    });
  }

  get f() {
    return this.loginForm.controls;
  }

  protected hasError(fieldName: string): boolean {
    const field = this.loginForm.get(fieldName);
    return !!(field?.invalid && field?.touched);
  }
}
