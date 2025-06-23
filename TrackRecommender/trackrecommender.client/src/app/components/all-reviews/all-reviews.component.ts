import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { ReviewService } from '../../services/review/review.service';
import { MapService } from '../../services/map/map.service';
import { TrailService } from '../../services/trail/trail.service';
import {
  TrailReview,
  ReviewFilters,
  ReviewsResponse,
} from '../../models/review.model';
import { animate, style, transition, trigger } from '@angular/animations';

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
  protected isLoading = true;
  protected error: string | null = null;

  protected currentPage = 1;
  protected pageSize = 20;
  protected totalCount = 0;
  protected totalPages = 0;

  protected showFilters = false;
  protected activeFiltersCount = 0;
  protected filters: ReviewFilters = {
    rating: null,
    hasCompleted: null,
    perceivedDifficulty: null,
    startDate: undefined,
    endDate: undefined,
    trailId: undefined,
    userId: undefined,
  };

  protected sortBy: 'date-desc' | 'date-asc' | 'rating-desc' | 'rating-asc' =
    'date-desc';

  protected searchTerm = '';
  protected filteredReviews: TrailReview[] = [];

  protected averageRating = 0;
  protected completionRate = 0;

  constructor(
    private reviewService: ReviewService,
    private trailService: TrailService,
    private mapService: MapService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadReviews();
  }

  protected loadReviews(): void {
    this.isLoading = true;
    this.error = null;

    this.reviewService
      .getAllReviews(this.filters, this.currentPage, this.pageSize)
      .subscribe({
        next: (response: ReviewsResponse) => {
          this.reviews = response.reviews;
          this.totalCount = response.totalCount;
          this.totalPages = Math.ceil(response.totalCount / this.pageSize);
          this.calculateStats();
          this.applyLocalFilters();
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error loading reviews:', error);
          this.error = 'Failed to load reviews. Please try again.';
          this.isLoading = false;
        },
      });
  }

  protected calculateStats(): void {
    if (this.reviews.length === 0) {
      this.averageRating = 0;
      this.completionRate = 0;
      return;
    }

    const totalRating = this.reviews.reduce(
      (sum, review) => sum + review.rating,
      0
    );
    this.averageRating = totalRating / this.reviews.length;

    const completedCount = this.reviews.filter((r) => r.hasCompleted).length;
    this.completionRate = (completedCount / this.reviews.length) * 100;
  }

  protected applyLocalFilters(): void {
    let filtered = [...this.reviews];

    if (this.searchTerm.trim()) {
      const search = this.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (review) =>
          review.trailName.toLowerCase().includes(search) ||
          review.username.toLowerCase().includes(search) ||
          (review.comment && review.comment.toLowerCase().includes(search))
      );
    }

    filtered.sort((a, b) => {
      switch (this.sortBy) {
        case 'date-asc':
          return new Date(a.ratedAt).getTime() - new Date(b.ratedAt).getTime();
        case 'date-desc':
          return new Date(b.ratedAt).getTime() - new Date(a.ratedAt).getTime();
        case 'rating-asc':
          return a.rating - b.rating;
        case 'rating-desc':
          return b.rating - a.rating;
        default:
          return 0;
      }
    });

    this.filteredReviews = filtered;
  }

  protected onFiltersChange(): void {
    this.activeFiltersCount = 0;
    if (this.filters.rating !== null) this.activeFiltersCount++;
    if (this.filters.hasCompleted !== null) this.activeFiltersCount++;
    if (this.filters.perceivedDifficulty) this.activeFiltersCount++;
    if (this.filters.startDate) this.activeFiltersCount++;
    if (this.filters.endDate) this.activeFiltersCount++;

    this.currentPage = 1;
    this.loadReviews();
  }

  protected onSearchChange(): void {
    this.applyLocalFilters();
  }

  protected onSortChange(): void {
    this.applyLocalFilters();
  }

  protected clearFilters(): void {
    this.filters = {
      rating: null,
      hasCompleted: null,
      perceivedDifficulty: null,
      startDate: undefined,
      endDate: undefined,
      trailId: undefined,
      userId: undefined,
    };
    this.searchTerm = '';
    this.onFiltersChange();
  }

  protected goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.loadReviews();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  protected nextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.goToPage(this.currentPage + 1);
    }
  }

  protected previousPage(): void {
    if (this.currentPage > 1) {
      this.goToPage(this.currentPage - 1);
    }
  }

  protected getPageNumbers(): number[] {
    const pages: number[] = [];
    const maxPagesToShow = 7;
    const halfRange = Math.floor(maxPagesToShow / 2);

    let startPage = Math.max(1, this.currentPage - halfRange);
    let endPage = Math.min(this.totalPages, this.currentPage + halfRange);

    if (this.currentPage <= halfRange) {
      endPage = Math.min(maxPagesToShow, this.totalPages);
    } else if (this.currentPage + halfRange >= this.totalPages) {
      startPage = Math.max(1, this.totalPages - maxPagesToShow + 1);
    }

    for (let i = startPage; i <= endPage; i++) {
      pages.push(i);
    }

    return pages;
  }

  protected viewTrailOnMap(trailId: number): void {
    this.router.navigate(['/map'], {
      queryParams: {
        trailId: trailId,
        mode: 'trail-focus',
      },
    });
  }

  protected getStarArray(rating: number): boolean[] {
    return Array(5)
      .fill(false)
      .map((_, i) => i < rating);
  }

  protected getDifficultyColor(difficulty?: string): string {
    if (!difficulty) return '#666';

    switch (difficulty.toLowerCase()) {
      case 'easy':
        return '#28a745';
      case 'moderate':
        return '#ffc107';
      case 'hard':
        return '#fd7e14';
      case 'very hard':
        return '#dc3545';
      default:
        return '#666';
    }
  }

  protected formatDuration(hours?: number): string {
    if (!hours) return '';

    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);

    if (h === 0) {
      return `${m}min`;
    } else if (m === 0) {
      return `${h}h`;
    } else {
      return `${h}h ${m}min`;
    }
  }

  protected formatDate(date: Date | string): string {
    const d = new Date(date);
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) {
      return 'Today';
    } else if (diffDays === 1) {
      return 'Yesterday';
    } else if (diffDays < 7) {
      return `${diffDays} days ago`;
    } else if (diffDays < 30) {
      const weeks = Math.floor(diffDays / 7);
      return `${weeks} week${weeks > 1 ? 's' : ''} ago`;
    } else if (diffDays < 365) {
      const months = Math.floor(diffDays / 30);
      return `${months} month${months > 1 ? 's' : ''} ago`;
    } else {
      const years = Math.floor(diffDays / 365);
      return `${years} year${years > 1 ? 's' : ''} ago`;
    }
  }

  protected getDisplayCount(
    page: number,
    pageSize: number,
    total: number
  ): number {
    return Math.min(page * pageSize, total);
  }
}
