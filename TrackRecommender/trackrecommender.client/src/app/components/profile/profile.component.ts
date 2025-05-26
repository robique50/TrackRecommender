import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth/auth.service';
import { UserProfile } from '../../models/auth.model';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
  standalone: true,
  imports: [CommonModule, RouterModule, MainNavbarComponent],
})
export class ProfileComponent implements OnInit {
  userProfile: UserProfile | null = null;
  isLoading = true;
  error: string | null = null;

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.loadUserProfile();
  }

  private loadUserProfile(): void {
    this.authService.currentUser$.subscribe((user) => {
      if (user) {
        this.userProfile = user;
        this.isLoading = false;
      } else {
        this.fetchUserProfile();
      }
    });
  }

  private fetchUserProfile(): void {
    this.authService.getUserProfile().subscribe({
      next: (profile) => {
        this.userProfile = profile;
        this.isLoading = false;
        this.error = null;
      },
      error: (error) => {
        console.error('Error loading user profile:', error);
        this.error = 'Nu am putut încărca profilul. Te rugăm încearcă din nou.';
        this.isLoading = false;
      },
    });
  }

  getInitial(): string {
    if (!this.userProfile?.username) {
      return '?';
    }
    return this.userProfile.username.charAt(0).toUpperCase();
  }

  refreshProfile(): void {
    this.isLoading = true;
    this.error = null;
    this.fetchUserProfile();
  }
}
