import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, FormControl, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth/auth.service';
import { TokenStorageService } from '../../services/token-storage/token-storage.service';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule]
})
export class RegisterComponent implements OnInit {
  registerForm: FormGroup;
  isLoading = false;
  error: string | null = null;
  success = false;
  
  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private tokenStorage: TokenStorageService,
    private router: Router
  ) {
    this.registerForm = this.fb.group({
      username: new FormControl('', [Validators.required, Validators.minLength(3)]),
      email: new FormControl('', [Validators.required, Validators.email]),
      password: new FormControl('', [
        Validators.required, 
        Validators.minLength(8),
        Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$/)
      ]),
      confirmPassword: new FormControl('', [Validators.required])
    });
    
    this.registerForm.addValidators(this.passwordMatchValidator);
  }
  
  private passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
    const password = control.get('password')?.value;
    const confirmPassword = control.get('confirmPassword')?.value;
    
    if (password === confirmPassword) {
      control.get('confirmPassword')?.setErrors(null);
      return null;
    } else {
      control.get('confirmPassword')?.setErrors({ mismatch: true });
      return { mismatch: true };
    }
  }

  ngOnInit(): void {
    if (this.tokenStorage.hasValidToken()) {
      this.router.navigate(['/map']);
    }
  }
  
  protected onSubmit() {
    if (this.registerForm.invalid) {
      return;
    }
    
    this.isLoading = true;
    this.error = null;
    
    this.authService.register(this.registerForm.value).subscribe({
      next: () => {
        this.isLoading = false;
        this.success = true;
        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 2000);
      },
      error: (err) => {
        this.isLoading = false;
        this.error = err;
      }
    });
  }
  
  get f() { return this.registerForm.controls; }
  
  protected hasUpperCase(): boolean {
    const passwordValue = this.f['password'].value;
    return passwordValue ? /[A-Z]/.test(passwordValue) : false;
  }
  
  protected hasLowerCase(): boolean {
    const passwordValue = this.f['password'].value;
    return passwordValue ? /[a-z]/.test(passwordValue) : false;
  }
  
  protected hasDigit(): boolean {
    const passwordValue = this.f['password'].value;
    return passwordValue ? /\d/.test(passwordValue) : false;
  }
  
  protected hasSpecialChar(): boolean {
    const passwordValue = this.f['password'].value;
    return passwordValue ? /[^\da-zA-Z]/.test(passwordValue) : false;
  }
  
  protected hasMinLength(): boolean {
    const passwordValue = this.f['password'].value;
    return passwordValue ? passwordValue.length >= 8 : false;
  }
}
