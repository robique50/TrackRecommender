import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { ReviewService } from '../../services/review/review.service';
import { MapService } from '../../services/map/map.service';
import { TrailService } from '../../services/trail/trail.service';
import { TrailReview } from '../../models/review.model';
import { animate, style, transition, trigger } from '@angular/animations';

interface ReviewFilters {
  rating: number | null;
  hasCompleted: boolean | null;
  perceivedDifficulty: string | null;
  dateRange: string;
  searchTerm: string;
}

@Component({
  selector: 'app-all-reviews',
  standalone: true,
  imports: [CommonModule, FormsModule, MainNavbarComponent],
  templateUrl: './all-reviews.component.html',
  styleUrls: ['./all-reviews.component.scss'],
  animations: [
    trigger('slideDown', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-20px)' }),
        animate(
          '300ms ease-out',
          style({ opacity: 1, transform: 'translateY(0)' })
        ),
      ]),
    ]),
  ],
})
export class AllReviewsComponent implements OnInit {
  protected reviews: TrailReview[] = [];
  protected filteredReviews: TrailReview[] = [];
  protected isLoading = true;
  protected error: string | null = null;

  protected showFilters = false;
  protected filters: ReviewFilters = {
    rating: null,
    hasCompleted: null,
    perceivedDifficulty: null,
    dateRange: 'all',
    searchTerm: '',
  };

  protected sortBy: 'date-desc' | 'date-asc' | 'rating-desc' | 'rating-asc' =
    'date-desc';

  protected currentPage = 1;
  protected reviewsPerPage = 10;
  protected totalPages = 0;

  protected totalReviews = 0;
  protected averageRating = 0;
  protected completionRate = 0;
  protected difficultyDistribution: { [key: string]: number } = {};

  constructor(
    private reviewService: ReviewService,
    private trailService: TrailService,
    private mapService: MapService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadAllReviews();
  }

  protected loadAllReviews(): void {
    this.isLoading = true;
    this.error = null;

    this.reviewService.getRecentReviews(1000).subscribe({
      next: (reviews) => {
        this.reviews = reviews;
        this.calculateStats();
        this.applyFilters();
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading reviews:', error);
        this.error = 'Failed to load reviews. Please try again.';
        this.isLoading = false;
      },
    });
  }

  private calculateStats(): void {
    this.totalReviews = this.reviews.length;

    if (this.totalReviews > 0) {
      const sum = this.reviews.reduce((acc, review) => acc + review.rating, 0);
      this.averageRating = sum / this.totalReviews;

      const completed = this.reviews.filter((r) => r.hasCompleted).length;
      this.completionRate = (completed / this.totalReviews) * 100;

      this.difficultyDistribution = {};
      this.reviews.forEach((review) => {
        if (review.perceivedDifficulty) {
          this.difficultyDistribution[review.perceivedDifficulty] =
            (this.difficultyDistribution[review.perceivedDifficulty] || 0) + 1;
        }
      });
    }
  }

  protected applyFilters(): void {
    let filtered = [...this.reviews];

    if (this.filters.rating !== null) {
      filtered = filtered.filter((r) => r.rating === this.filters.rating);
    }

    if (this.filters.hasCompleted !== null) {
      filtered = filtered.filter(
        (r) => r.hasCompleted === this.filters.hasCompleted
      );
    }

    if (this.filters.perceivedDifficulty) {
      filtered = filtered.filter(
        (r) => r.perceivedDifficulty === this.filters.perceivedDifficulty
      );
    }

    if (this.filters.dateRange !== 'all') {
      const now = new Date();
      const cutoffDate = new Date();

      switch (this.filters.dateRange) {
        case 'today':
          cutoffDate.setHours(0, 0, 0, 0);
          break;
        case 'week':
          cutoffDate.setDate(now.getDate() - 7);
          break;
        case 'month':
          cutoffDate.setMonth(now.getMonth() - 1);
          break;
        case 'year':
          cutoffDate.setFullYear(now.getFullYear() - 1);
          break;
      }

      filtered = filtered.filter((r) => new Date(r.ratedAt) >= cutoffDate);
    }

    if (this.filters.searchTerm) {
      const term = this.filters.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (r) =>
          r.trailName.toLowerCase().includes(term) ||
          r.username.toLowerCase().includes(term)
      );
    }

    this.sortReviews(filtered);

    this.filteredReviews = filtered;
    this.totalPages = Math.ceil(filtered.length / this.reviewsPerPage);
    this.currentPage = 1;
  }

  private sortReviews(reviews: TrailReview[]): void {
    switch (this.sortBy) {
      case 'date-desc':
        reviews.sort(
          (a, b) =>
            new Date(b.ratedAt).getTime() - new Date(a.ratedAt).getTime()
        );
        break;
      case 'date-asc':
        reviews.sort(
          (a, b) =>
            new Date(a.ratedAt).getTime() - new Date(b.ratedAt).getTime()
        );
        break;
      case 'rating-desc':
        reviews.sort((a, b) => b.rating - a.rating);
        break;
      case 'rating-asc':
        reviews.sort((a, b) => a.rating - b.rating);
        break;
    }
  }

  protected get paginatedReviews(): TrailReview[] {
    const start = (this.currentPage - 1) * this.reviewsPerPage;
    const end = start + this.reviewsPerPage;
    return this.filteredReviews.slice(start, end);
  }

  protected toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  protected resetFilters(): void {
    this.filters = {
      rating: null,
      hasCompleted: null,
      perceivedDifficulty: null,
      dateRange: 'all',
      searchTerm: '',
    };
    this.applyFilters();
  }

  protected onSortChange(): void {
    this.applyFilters();
  }

  protected onPageChange(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
    }
  }

  protected viewTrailOnMap(review: TrailReview): void {
    this.trailService.getTrailById(review.trailId).subscribe({
      next: (trail) => {
        this.mapService.setSelectedTrail(trail);
        this.router.navigate(['/map'], {
          queryParams: {
            trailId: trail.id,
            mode: 'trail-focus',
          },
        });
      },
      error: (error) => {
        console.error('Error loading trail:', error);
      },
    });
  }

  protected viewUserProfile(userId: number): void {
    console.log('View user profile:', userId);
  }

  protected getStarArray(rating: number): boolean[] {
    return this.reviewService.getStarArray(rating);
  }

  protected getDifficultyColor(difficulty?: string): string {
    return this.reviewService.getDifficultyColor(difficulty);
  }

  protected formatDuration(hours?: number): string {
    return this.reviewService.formatDuration(hours);
  }

  protected getPageNumbers(): number[] {
    const pages: number[] = [];
    const maxVisible = 5;

    let start = Math.max(1, this.currentPage - Math.floor(maxVisible / 2));
    let end = Math.min(this.totalPages, start + maxVisible - 1);

    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }

    return pages;
  }

  protected trackByReviewId(index: number, review: TrailReview): number {
    return review.id;
  }
}
