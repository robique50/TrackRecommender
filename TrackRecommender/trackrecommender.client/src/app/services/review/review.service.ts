import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  TrailReview,
  CreateReviewRequest,
  TrailRatingStats,
  CanReviewResponse,
  ReviewFilters,
  ReviewsResponse,
} from '../../models/review.model';

@Injectable({
  providedIn: 'root',
})
export class ReviewService {
  private readonly baseUrl = '/api/reviews';

  constructor(private http: HttpClient) {}

  public getMyReviews(): Observable<TrailReview[]> {
    return this.http.get<TrailReview[]>(`${this.baseUrl}/my-reviews`);
  }

  public getTrailReviews(trailId: number): Observable<TrailReview[]> {
    return this.http.get<TrailReview[]>(`${this.baseUrl}/trail/${trailId}`);
  }

  public getRecentReviews(count: number = 10): Observable<TrailReview[]> {
    const params = new HttpParams().set('count', count.toString());
    return this.http.get<TrailReview[]>(`${this.baseUrl}/recent`, { params });
  }

  public getTrailRatingStats(trailId: number): Observable<TrailRatingStats> {
    return this.http.get<TrailRatingStats>(
      `${this.baseUrl}/trail/${trailId}/stats`
    );
  }

  public getAllReviews(
    filters?: ReviewFilters,
    page: number = 1,
    pageSize: number = 20
  ): Observable<ReviewsResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (filters) {
      if (filters.rating !== null && filters.rating !== undefined) {
        params = params.set('rating', filters.rating.toString());
      }
      if (filters.hasCompleted !== null && filters.hasCompleted !== undefined) {
        params = params.set('hasCompleted', filters.hasCompleted.toString());
      }
      if (filters.perceivedDifficulty) {
        params = params.set('perceivedDifficulty', filters.perceivedDifficulty);
      }
      if (filters.startDate) {
        params = params.set('startDate', filters.startDate.toISOString());
      }
      if (filters.endDate) {
        params = params.set('endDate', filters.endDate.toISOString());
      }
      if (filters.trailId) {
        params = params.set('trailId', filters.trailId.toString());
      }
      if (filters.userId) {
        params = params.set('userId', filters.userId.toString());
      }
    }

    return this.http.get<ReviewsResponse>(`${this.baseUrl}/all`, { params });
  }

  public getReviewStatistics(): Observable<{
    totalReviews: number;
    averageRating: number;
    completionRate: number;
    difficultyDistribution: { [key: string]: number };
    ratingDistribution: { [rating: number]: number };
  }> {
    return this.http.get<any>(`${this.baseUrl}/statistics`);
  }

  public createReview(
    trailId: number,
    review: CreateReviewRequest
  ): Observable<TrailReview> {
    return this.http.post<TrailReview>(
      `${this.baseUrl}/trail/${trailId}`,
      review
    );
  }

  public getMyTrailReview(trailId: number): Observable<TrailReview> {
    return this.http.get<TrailReview>(
      `${this.baseUrl}/trail/${trailId}/my-review`
    );
  }

  public deleteReview(reviewId: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/${reviewId}`);
  }

  public canReviewTrail(trailId: number): Observable<CanReviewResponse> {
    return this.http.get<CanReviewResponse>(
      `${this.baseUrl}/trail/${trailId}/can-review`
    );
  }

  public getStarArray(rating: number): boolean[] {
    return Array(5)
      .fill(false)
      .map((_, index) => index < rating);
  }

  public getDifficultyColor(difficulty?: string): string {
    switch (difficulty?.toLowerCase()) {
      case 'easy':
        return '#4CAF50';
      case 'moderate':
        return '#FFC107';
      case 'difficult':
        return '#FF9800';
      case 'very difficult':
        return '#F44336';
      case 'expert':
        return '#9C27B0';
      default:
        return '#9E9E9E';
    }
  }

  public formatDuration(hours?: number): string {
    if (!hours) return 'N/A';

    const wholeHours = Math.floor(hours);
    const minutes = Math.round((hours - wholeHours) * 60);

    if (wholeHours === 0) {
      return `${minutes}m`;
    } else if (minutes === 0) {
      return `${wholeHours}h`;
    } else {
      return `${wholeHours}h ${minutes}m`;
    }
  }

  public getDateRangePresets(): { [key: string]: { start: Date; end: Date } } {
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    return {
      today: {
        start: today,
        end: now,
      },
      week: {
        start: new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000),
        end: now,
      },
      month: {
        start: new Date(today.getTime() - 30 * 24 * 60 * 60 * 1000),
        end: now,
      },
      year: {
        start: new Date(today.getTime() - 365 * 24 * 60 * 60 * 1000),
        end: now,
      },
    };
  }
}
