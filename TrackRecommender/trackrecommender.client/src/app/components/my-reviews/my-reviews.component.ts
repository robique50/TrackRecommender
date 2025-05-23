import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { ReviewService } from '../../services/review/review.service';
import { TrailReview } from '../../models/review.models';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';

@Component({
  selector: 'app-my-reviews',
  templateUrl: './my-reviews.component.html',
  styleUrl: './my-reviews.component.scss',
  standalone: true,
  imports: [CommonModule, RouterModule, MainNavbarComponent],
})
export class MyReviewsComponent implements OnInit {
  reviews: TrailReview[] = [];
  isLoading = true;
  isDeletingReview: number | null = null;
  error: string | null = null;

  constructor(
    private reviewService: ReviewService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadMyReviews();
  }

  get averageRating(): number {
    if (this.reviews.length === 0) return 0;
    const sum = this.reviews.reduce((acc, review) => acc + review.rating, 0);
    return sum / this.reviews.length;
  }

  get completedCount(): number {
    return this.reviews.filter((review) => review.hasCompleted).length;
  }

  private loadMyReviews(): void {
    this.isLoading = true;
    this.error = null;

    this.reviewService.getMyReviews().subscribe({
      next: (reviews) => {
        this.reviews = reviews;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading reviews:', error);
        this.error = 'Failed to load your reviews. Please try again.';
        this.isLoading = false;
      },
    });
  }

  deleteReview(review: TrailReview): void {
    if (
      !confirm(
        `Are you sure you want to delete your review for "${review.trailName}"?`,
      )
    ) {
      return;
    }

    this.isDeletingReview = review.id;

    this.reviewService.deleteReview(review.id).subscribe({
      next: () => {
        this.reviews = this.reviews.filter((r) => r.id !== review.id);
        this.isDeletingReview = null;
      },
      error: (error) => {
        console.error('Error deleting review:', error);
        this.error = 'Failed to delete review. Please try again.';
        this.isDeletingReview = null;
      },
    });
  }

  viewTrail(trailId: number): void {
    this.router.navigate(['/trails', trailId]);
  }

  getStarArray(rating: number): boolean[] {
    return this.reviewService.getStarArray(rating);
  }

  getDifficultyColor(difficulty?: string): string {
    return this.reviewService.getDifficultyColor(difficulty);
  }

  formatDuration(hours?: number): string {
    return this.reviewService.formatDuration(hours);
  }

  trackByReviewId(index: number, review: TrailReview): number {
    return review.id;
  }
}
