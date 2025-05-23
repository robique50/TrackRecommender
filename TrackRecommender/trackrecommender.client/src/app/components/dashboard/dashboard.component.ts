import {
  Component,
  OnInit,
  ViewChild,
  ElementRef,
  AfterViewInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth/auth.service';
import { UserProfile } from '../../models/auth.models';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { WeatherWidgetComponent } from '../weather-widget/weather-widget.component';

interface ForecastDay {
  name: string;
  iconUrl: string;
  tempMax: number;
  tempMin: number;
  condition: string;
}

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MainNavbarComponent,
    WeatherWidgetComponent,
  ],
})
export class DashboardComponent implements OnInit, AfterViewInit {
  @ViewChild('daysContainer') daysContainer!: ElementRef;
  userProfile: UserProfile | null = null;
  forecastDays: ForecastDay[] = [];

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe((user) => {
      this.userProfile = user;
    });
  }

  ngAfterViewInit(): void {}
}
