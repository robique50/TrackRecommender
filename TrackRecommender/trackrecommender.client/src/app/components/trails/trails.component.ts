import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { TrailService } from '../../services/trail/trail.service';
import { MapService } from '../../services/map/map.service';
import { Trail } from '../../models/trail.model';
import { animate, style, transition, trigger } from '@angular/animations';

interface TrailFilters {
  difficulty: string | null;
  minRating: number | null;
  maxDistance: number | null;
  maxDuration: number | null;
  trailType: string | null;
  hasReviews: boolean | null;
  searchTerm: string;
}

@Component({
  selector: 'app-trails',
  standalone: true,
  imports: [CommonModule, FormsModule, MainNavbarComponent],
  templateUrl: './trails.component.html',
  styleUrl: './trails.component.scss',
  animations: [
    trigger('slideDown', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-10px)' }),
        animate(
          '300ms ease-out',
          style({ opacity: 1, transform: 'translateY(0)' })
        ),
      ]),
    ]),
  ],
})
export class TrailsComponent implements OnInit {
  protected trails: Trail[] = [];
  protected filteredTrails: Trail[] = [];
  protected isLoading = false;
  protected error: string | null = null;

  protected showFilters = false;
  protected filters: TrailFilters = {
    difficulty: null,
    minRating: null,
    maxDistance: null,
    maxDuration: null,
    trailType: null,
    hasReviews: null,
    searchTerm: '',
  };

  protected sortBy: 'name' | 'distance' | 'duration' | 'rating' | 'reviews' =
    'name';
  protected sortOrder: 'asc' | 'desc' = 'asc';

  protected difficulties = [
    'Easy',
    'Moderate',
    'Difficult',
    'Very Difficult',
    'Expert',
  ];
  protected trailTypes: string[] = [];

  protected totalTrails = 0;
  protected averageRating = 0;
  protected totalReviews = 0;

  constructor(
    private trailService: TrailService,
    private mapService: MapService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadTrails();
  }

  protected loadTrails(): void {
    this.isLoading = true;
    this.error = null;

    this.trailService.getTrails().subscribe({
      next: (trails) => {
        this.trails = trails;
        this.extractTrailTypes();
        this.calculateStats();
        this.applyFilters();
        this.isLoading = false;
      },
      error: (error) => {
        this.error = 'Failed to load trails. Please try again.';
        this.isLoading = false;
      },
    });
  }

  private extractTrailTypes(): void {
    const types = new Set<string>();
    this.trails.forEach((trail) => {
      if (trail.trailType) {
        types.add(trail.trailType);
      }
    });
    this.trailTypes = Array.from(types).sort();
  }

  private calculateStats(): void {
    this.totalTrails = this.trails.length;

    let totalRating = 0;
    let ratedTrails = 0;
    this.totalReviews = 0;

    this.trails.forEach((trail) => {
      if (trail.averageRating && trail.averageRating > 0) {
        totalRating += trail.averageRating;
        ratedTrails++;
      }
      this.totalReviews += trail.reviewsCount || 0;
    });

    this.averageRating = ratedTrails > 0 ? totalRating / ratedTrails : 0;
  }

  protected applyFilters(): void {
    let filtered = [...this.trails];

    if (this.filters.difficulty) {
      filtered = filtered.filter(
        (t) => t.difficulty === this.filters.difficulty
      );
    }

    if (this.filters.minRating !== null) {
      filtered = filtered.filter(
        (t) => t.averageRating && t.averageRating >= this.filters.minRating!
      );
    }

    if (this.filters.maxDistance !== null) {
      filtered = filtered.filter(
        (t) => t.distance <= this.filters.maxDistance!
      );
    }

    if (this.filters.maxDuration !== null) {
      filtered = filtered.filter(
        (t) => t.duration <= this.filters.maxDuration!
      );
    }

    if (this.filters.trailType) {
      filtered = filtered.filter((t) => t.trailType === this.filters.trailType);
    }

    if (this.filters.hasReviews !== null) {
      filtered = filtered.filter((t) =>
        this.filters.hasReviews
          ? (t.reviewsCount || 0) > 0
          : (t.reviewsCount || 0) === 0
      );
    }

    if (this.filters.searchTerm) {
      const term = this.filters.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (t) =>
          t.name.toLowerCase().includes(term) ||
          t.regionNames.some((r) => r.toLowerCase().includes(term)) ||
          (t.description && t.description.toLowerCase().includes(term))
      );
    }

    this.sortTrails(filtered);

    this.filteredTrails = filtered;
  }

  private sortTrails(trails: Trail[]): void {
    trails.sort((a, b) => {
      let comparison = 0;

      switch (this.sortBy) {
        case 'name':
          comparison = a.name.localeCompare(b.name);
          break;
        case 'distance':
          comparison = a.distance - b.distance;
          break;
        case 'duration':
          comparison = a.duration - b.duration;
          break;
        case 'rating':
          comparison = (b.averageRating || 0) - (a.averageRating || 0);
          break;
        case 'reviews':
          comparison = (b.reviewsCount || 0) - (a.reviewsCount || 0);
          break;
      }

      return this.sortOrder === 'asc' ? comparison : -comparison;
    });
  }

  protected toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  protected resetFilters(): void {
    this.filters = {
      difficulty: null,
      minRating: null,
      maxDistance: null,
      maxDuration: null,
      trailType: null,
      hasReviews: null,
      searchTerm: '',
    };
    this.applyFilters();
  }

  protected onSortChange(field: typeof this.sortBy): void {
    if (this.sortBy === field) {
      this.sortOrder = this.sortOrder === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = field;
      this.sortOrder = 'asc';
    }
    this.applyFilters();
  }

  protected viewTrailOnMap(trail: Trail): void {
    this.mapService.setSelectedTrail(trail);
    console.log('Navigating to map with trail:', trail);
    this.router.navigate(['/map'], {
      queryParams: {
        trailId: trail.id,
        mode: 'trail-focus',
      },
    });
  }

  protected getStarArray(rating: number): boolean[] {
    return Array(5)
      .fill(false)
      .map((_, index) => index < Math.round(rating));
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
    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);
    return m > 0 ? `${h}h ${m}min` : `${h}h`;
  }

  protected getGradientClass(difficulty: string): string {
    const classes: { [key: string]: string } = {
      Easy: 'easy-gradient',
      Moderate: 'moderate-gradient',
      Difficult: 'difficult-gradient',
      'Very Difficult': 'very-difficult-gradient',
      Expert: 'expert-gradient',
    };
    return classes[difficulty] || '';
  }

  protected getDifficultyClass(difficulty: string): string {
    const classes: { [key: string]: string } = {
      Easy: 'easy',
      Moderate: 'moderate',
      Difficult: 'difficult',
      'Very Difficult': 'very-difficult',
      Expert: 'expert',
    };
    return classes[difficulty] || '';
  }

  protected trackByTrailId(index: number, trail: Trail): number {
    return trail.id;
  }
}
