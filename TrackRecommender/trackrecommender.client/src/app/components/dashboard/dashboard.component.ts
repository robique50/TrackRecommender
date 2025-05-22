import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth/auth.service';
import { UserProfile } from '../../models/auth.models';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { WeatherWidgetComponent } from '../weather-widget/weather-widget.component';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  standalone: true,
  imports: [CommonModule, RouterModule, MainNavbarComponent, WeatherWidgetComponent]
})
export class DashboardComponent implements OnInit {
  userProfile: UserProfile | null = null;

  constructor(private authService: AuthService) { }

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      this.userProfile = user;
    });
  }
}
