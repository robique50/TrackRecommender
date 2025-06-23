// Updated dashboard.component.ts
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
import { UserProfile } from '../../models/auth.model';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { WeatherWidgetComponent } from '../weather-widget/weather-widget.component';
import { ForecastDay } from '../../models/weather.model';
import { TrailService } from '../../services/trail/trail.service';
import { ReviewService } from '../../services/review/review.service';
import { forkJoin } from 'rxjs';

interface QuickStat {
  icon: string;
  label: string;
  value: string | number;
  color: string;
  route?: string;
}

interface FeatureCard {
  icon: string;
  title: string;
  description: string;
  route: string;
  color: string;
  emoji: string;
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
export class DashboardComponent implements OnInit {
  userProfile: UserProfile | null = null;
  currentHour = new Date().getHours();
  greeting = '';

  // Stats
  quickStats: QuickStat[] = [];
  isLoadingStats = true;

  // Features
  features: FeatureCard[] = [
    {
      icon: 'fas fa-map-marked-alt',
      title: 'Explore Trails',
      description:
        'Discover Romanian hiking trails on an interactive map with detailed information and difficulty ratings.',
      route: '/map',
      color: '#2e5d32',
      emoji: '🗺️', // Adaugă emoji ca backup
    },
    {
      icon: 'fas fa-compass',
      title: 'Get Recommendations',
      description:
        'Receive personalized trail suggestions based on your preferences and fitness level.',
      route: '/recommendations',
      color: '#1976d2',
      emoji: '🧭',
    },
    {
      icon: 'fas fa-star',
      title: 'Write Reviews',
      description:
        'Share your hiking experiences and help others discover amazing trails.',
      route: '/trails',
      color: '#f57c00',
      emoji: '⭐',
    },
    {
      icon: 'fas fa-user-cog',
      title: 'Set Preferences',
      description:
        'Customize your profile to get better trail recommendations tailored to your hiking style.',
      route: '/preferences',
      color: '#7b1fa2',
      emoji: '⚙️',
    },
  ];

  // Tips
  hikingTips = [
    'Always check the weather forecast before heading out',
    'Bring enough water - at least 2L for a full day hike',
    'Tell someone about your hiking plans and expected return',
    'Start with easier trails and gradually increase difficulty',
    'Respect nature - take only photos, leave only footprints',
  ];

  currentTipIndex = 0;

  constructor(
    private authService: AuthService,
    private trailService: TrailService,
    private reviewService: ReviewService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe((user) => {
      this.userProfile = user;
      this.setGreeting();
      if (user) {
        this.loadUserStats();
      }
    });

    // Rotate tips every 5 seconds
    setInterval(() => {
      this.currentTipIndex =
        (this.currentTipIndex + 1) % this.hikingTips.length;
    }, 5000);
  }

  private setGreeting(): void {
    if (this.currentHour < 12) {
      this.greeting = 'Good morning';
    } else if (this.currentHour < 18) {
      this.greeting = 'Good afternoon';
    } else {
      this.greeting = 'Good evening';
    }
  }

  private loadUserStats(): void {
    this.isLoadingStats = true;

    forkJoin({
      trails: this.trailService.getTrails(),
      reviews: this.reviewService.getMyReviews(),
    }).subscribe({
      next: ({ trails, reviews }) => {
        const completedTrails = reviews.filter((r) => r.hasCompleted).length;
        const totalDistance = reviews
          .filter((r) => r.hasCompleted && r.actualDuration)
          .reduce((sum, r) => sum + (r.actualDuration || 0) * 4, 0); // Approximate 4km/h

        this.quickStats = [
          {
            icon: 'fas fa-route',
            label: 'Available Trails',
            value: trails.length,
            color: '#2e5d32',
            route: '/trails',
          },
          {
            icon: 'fas fa-check-circle',
            label: 'Completed',
            value: completedTrails,
            color: '#4caf50',
          },
          {
            icon: 'fas fa-shoe-prints',
            label: 'Total Distance',
            value: `${Math.round(totalDistance)} km`,
            color: '#ff9800',
          },
          {
            icon: 'fas fa-star',
            label: 'Reviews Written',
            value: reviews.length,
            color: '#2196f3',
            route: '/my-reviews',
          },
        ];

        this.isLoadingStats = false;
      },
      error: () => {
        this.isLoadingStats = false;
        // Show default stats on error
        this.quickStats = [
          {
            icon: 'fas fa-route',
            label: 'Available Trails',
            value: '50+',
            color: '#2e5d32',
            route: '/trails',
          },
        ];
      },
    });
  }

  get currentTip(): string {
    return this.hikingTips[this.currentTipIndex];
  }
}
