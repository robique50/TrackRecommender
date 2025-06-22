import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { RecommendationService } from '../../services/recommendation/recommendation.service';
import { TrailRecommendation } from '../../models/trail-recommendation.model';
import { MapService } from '../../services/map/map.service';
import { RecommendationSettingsService } from '../../services/recommendation-settings/recommendation-settings.service';

@Component({
  selector: 'app-recommendations',
  standalone: true,
  imports: [CommonModule, MainNavbarComponent, FormsModule],
  templateUrl: './recommendations.component.html',
  styleUrls: ['./recommendations.component.scss'],
})
export class RecommendationsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  protected recommendations: TrailRecommendation[] = [];
  protected isLoading = false;
  protected error: string | null = null;
  protected expandedRecommendation: number | null = null;
  protected hasLoadedOnce = false;

  protected numberOfTrails: number = 10;
  protected includeWeather: boolean = true;
  protected showSettings: boolean = false;

  protected trailCountOptions = [5, 10, 15, 20, 25, 30];

  constructor(
    private recommendationService: RecommendationService,
    private recommendationSettingsService: RecommendationSettingsService,
    private mapService: MapService,
    private router: Router
  ) {
    const settings = this.recommendationSettingsService.getSettings();
    this.numberOfTrails = settings.count;
    this.includeWeather = settings.includeWeather;
  }

  ngOnInit(): void {
    const cached = this.recommendationService.getCachedRecommendations();
    if (cached && cached.length > 0) {
      this.recommendations = cached;
      this.hasLoadedOnce = true;
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  protected loadRecommendations(forceRefresh: boolean = false): void {
    this.isLoading = true;
    this.error = null;

    this.recommendationService
      .getRecommendations(
        this.numberOfTrails,
        this.includeWeather,
        forceRefresh
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.recommendations = response.recommendations;
          this.hasLoadedOnce = true;
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error loading recommendations:', error);
          this.error = 'Failed to load recommendations. Please try again.';
          this.isLoading = false;
        },
      });
  }

  protected viewTrailOnMap(trail: any): void {
    this.mapService.setSelectedTrail(trail);
    this.router.navigate(['/map'], {
      queryParams: {
        trailId: trail.id,
        mode: 'trail-focus',
      },
    });
  }

  protected toggleDetails(index: number): void {
    this.expandedRecommendation =
      this.expandedRecommendation === index ? null : index;
  }

  protected toggleSettings(): void {
    this.showSettings = !this.showSettings;
  }

  protected applySettings(): void {
    this.recommendationSettingsService.updateSettings({
      count: this.numberOfTrails,
      includeWeather: this.includeWeather,
    });
    this.showSettings = false;
    this.loadRecommendations(true);
  }

  protected cancelSettings(): void {
    const settings = this.recommendationSettingsService.getSettings();
    this.numberOfTrails = settings.count;
    this.includeWeather = settings.includeWeather;
    this.showSettings = false;
  }

  protected getDifficultyColor(difficulty: string): string {
    const colors: { [key: string]: string } = {
      Easy: '#4CAF50',
      Moderate: '#FFC107',
      Difficult: '#FF9800',
      'Very Difficult': '#F44336',
      Expert: '#9C27B0',
    };
    return colors[difficulty] || '#757575';
  }

  protected getScoreIcon(component: string): string {
    return this.recommendationService.getScoreIcon(component);
  }

  protected getScoreColor(score: number): string {
    return this.recommendationService.getScoreColor(score);
  }

  protected getRecommendations(): void {
    this.loadRecommendations(false);
  }

  protected formatDuration(hours: number): string {
    if (hours < 1) {
      return `${Math.round(hours * 60)} min`;
    }
    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);
    return m > 0 ? `${h}h ${m}min` : `${h}h`;
  }

  protected refreshRecommendations(): void {
    this.loadRecommendations(true);
  }

  protected hasWeatherScore(recommendation: TrailRecommendation): boolean {
    return (
      recommendation.scoreBreakdown &&
      'weather' in recommendation.scoreBreakdown
    );
  }
}
