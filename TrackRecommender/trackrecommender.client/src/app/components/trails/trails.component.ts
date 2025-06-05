import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { TrailService } from '../../services/trail/trail.service';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { Trail } from '../../models/trail.model';

@Component({
  selector: 'app-trails',
  standalone: true,
  imports: [CommonModule, MainNavbarComponent],
  templateUrl: './trails.component.html',
  styleUrl: './trails.component.scss',
})
export class TrailsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  protected trails: Trail[] = [];
  protected isLoading = false;
  protected error: string | null = null;

  constructor(private trailService: TrailService) {}

  ngOnInit(): void {
    this.loadTrails();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadTrails(): void {
    this.isLoading = true;
    this.error = null;

    this.trailService
      .getTrails()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (trails) => {
          this.trails = trails;
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error loading trails:', error);
          this.error = 'Failed to load trails. Please try again.';
          this.isLoading = false;
        },
      });
  }

  protected getPatternWidth(category: string): number {
    const widths: { [key: string]: number } = {
      International: 20,
      National: 16,
      Regional: 24,
      Local: 10,
    };
    return widths[category] || 10;
  }

  protected getPatternHeight(category: string): number {
    const heights: { [key: string]: number } = {
      International: 20,
      National: 16,
      Regional: 24,
      Local: 10,
    };
    return heights[category] || 10;
  }

  protected getTrailTypeIcon(trailType: string): string {
    const icons: { [key: string]: string } = {
      Hiking: '🥾',
      'Bicycle Trail': '🚴',
      'MTB Trail': '🚵',
      'Foot Trail': '🚶',
      'Equestrian Trail': '🐎',
      'Ski Trail': '🎿',
    };
    return icons[trailType] || '🏃';
  }

  protected getDifficultyBadgeClass(difficulty: string): string {
    const classes: { [key: string]: string } = {
      Easy: 'bg-green-100 text-green-800 border border-green-200',
      Moderate: 'bg-yellow-100 text-yellow-800 border border-yellow-200',
      Difficult: 'bg-orange-100 text-orange-800 border border-orange-200',
      'Very Difficult': 'bg-red-100 text-red-800 border border-red-200',
      Expert: 'bg-purple-100 text-purple-800 border border-purple-200',
    };
    return (
      classes[difficulty] || 'bg-blue-100 text-blue-800 border border-blue-200'
    );
  }

  public getPatternId(category: string): string {
    const patterns: { [key: string]: string } = {
      International: 'dots',
      National: 'grid',
      Regional: 'diagonal',
      Local: 'dots',
    };
    return patterns[category] || 'dots';
  }

  public formatDuration(hours: number): string {
    if (hours < 1) {
      return `${Math.round(hours * 60)}min`;
    }
    return `${hours}h`;
  }

  public onTrailClick(trail: Trail): void {
    console.log('Selected trail:', trail);
  }

  public trackByTrailId(index: number, trail: Trail): number {
    return trail.id;
  }

  public refreshTrails(): void {
    this.loadTrails();
  }

  protected getGradientClass(difficulty: string): string {
    const map: { [key: string]: string } = {
      Easy: 'easy-gradient',
      Moderate: 'moderate-gradient',
      Difficult: 'difficult-gradient',
      'Very Difficult': 'very-difficult-gradient',
      Expert: 'expert-gradient',
    };
    return map[difficulty] || 'easy-gradient';
  }

  protected getDifficultyClass(difficulty: string): string {
    const difficultyMap: { [key: string]: string } = {
      Easy: 'easy',
      Moderate: 'moderate',
      Difficult: 'difficult',
      'Very Difficult': 'very-difficult',
      Expert: 'expert',
    };
    return difficultyMap[difficulty] || 'easy';
  }
}
