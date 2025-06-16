import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  FormControl,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth/auth.service';
import { TokenStorageService } from '../../services/token-storage/token-storage.service';
import { AuthNavbarComponent } from '../auth-navbar/auth-navbar.component';
import { trigger, transition, style, animate } from '@angular/animations';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
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
    trigger('checkmark', [
      transition(':enter', [
        style({ transform: 'scale(0)' }),
        animate('300ms ease-out', style({ transform: 'scale(1)' })),
      ]),
    ]),
  ],
})
export class RegisterComponent implements OnInit {
  protected registerForm: FormGroup;
  protected isLoading = false;
  protected error: string | null = null;
  protected success = false;
  protected successMessage = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private tokenStorage: TokenStorageService,
    private router: Router
  ) {
    this.registerForm = this.fb.group(
      {
        username: new FormControl('', [
          Validators.required,
          Validators.minLength(3),
        ]),
        email: new FormControl('', [Validators.required, Validators.email]),
        password: new FormControl('', [
          Validators.required,
          Validators.minLength(8),
          Validators.pattern(
            /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$/
          ),
        ]),
        confirmPassword: new FormControl('', [Validators.required]),
      },
      { validators: this.passwordMatchValidator }
    );
  }

  ngOnInit(): void {
    if (this.tokenStorage.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    }
  }

  private passwordMatchValidator(
    control: AbstractControl
  ): ValidationErrors | null {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');

    if (!password || !confirmPassword) {
      return null;
    }

    return password.value === confirmPassword.value ? null : { mismatch: true };
  }

  protected onSubmit(): void {
    if (this.registerForm.invalid) {
      Object.keys(this.registerForm.controls).forEach((key) => {
        this.registerForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.isLoading = true;
    this.error = null;
    this.success = false;

    const { confirmPassword, ...registerData } = this.registerForm.value;

    this.authService.register(registerData).subscribe({
      next: () => {
        this.isLoading = false;
        this.success = true;
        this.successMessage =
          'Registration successful! Redirecting to login...';

        this.registerForm.reset();

        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 2000);
      },
      error: (err: any) => {
        this.isLoading = false;
        let displayMessage = 'Registration failed. Please try again.';

        if (typeof err === 'string' && err.trim() !== '') {
          displayMessage = err;
        } else if (err && err.message && typeof err.message === 'string') {
          displayMessage = err.message;
        } else if (err && err.error && typeof err.error === 'string') {
          displayMessage = err.error;
        } else if (
          err &&
          err.error &&
          err.error.errors &&
          typeof err.error.errors === 'object'
        ) {
          // Caz pentru ValidationProblemDetails (erori ModelState din ASP.NET Core)
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
    return this.registerForm.controls;
  }

  protected hasError(fieldName: string): boolean {
    const field = this.registerForm.get(fieldName);
    return !!(field?.invalid && field?.touched);
  }

  protected hasMinLength(): boolean {
    const password = this.f['password'].value;
    return password && password.length >= 8;
  }

  protected hasUpperCase(): boolean {
    const password = this.f['password'].value;
    return password && /[A-Z]/.test(password);
  }

  protected hasLowerCase(): boolean {
    const password = this.f['password'].value;
    return password && /[a-z]/.test(password);
  }

  protected hasDigit(): boolean {
    const password = this.f['password'].value;
    return password && /\d/.test(password);
  }

  protected hasSpecialChar(): boolean {
    const password = this.f['password'].value;
    return password && /[^\da-zA-Z]/.test(password);
  }
}
