import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { RecommendationService } from '../../services/recommendation/recommendation.service';
import { TrailRecommendation } from '../../models/trail-recommendation.model';
import { MapService } from '../../services/map/map.service';

@Component({
  selector: 'app-recommendations',
  standalone: true,
  imports: [CommonModule, MainNavbarComponent],
  templateUrl: './recommendations.component.html',
  styleUrls: ['./recommendations.component.scss'],
})
export class RecommendationsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  protected recommendations: TrailRecommendation[] = [];
  protected isLoading = false;
  protected error: string | null = null;
  protected expandedRecommendation: number | null = null;

  constructor(
    private recommendationService: RecommendationService,
    private mapService: MapService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadRecommendations();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  protected loadRecommendations(): void {
    this.isLoading = true;
    this.error = null;

    this.recommendationService
      .getRecommendations(10, true)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.recommendations = response.recommendations;
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error loading recommendations:', error);
          this.error = 'Failed to load recommendations. Please try again.';
          this.isLoading = false;
        },
      });
  }

  protected viewOnMap(trail: any): void {
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

  protected formatDuration(hours: number): string {
    if (hours < 1) {
      return `${Math.round(hours * 60)} min`;
    }
    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);
    return m > 0 ? `${h}h ${m}min` : `${h}h`;
  }

  protected refreshRecommendations(): void {
    this.loadRecommendations();
  }
}
