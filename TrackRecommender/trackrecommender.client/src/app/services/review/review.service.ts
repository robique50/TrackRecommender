import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TrailReview, CreateReviewRequest, TrailRatingStats, CanReviewResponse } from '../../models/review.models';

@Injectable({
  providedIn: 'root'
})
export class ReviewService {
  private readonly baseUrl = '/api/reviews';

  constructor(private http: HttpClient) { }

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
    return this.http.get<TrailRatingStats>(`${this.baseUrl}/trail/${trailId}/stats`);
  }

  public createReview(trailId: number, review: CreateReviewRequest): Observable<TrailReview> {
    return this.http.post<TrailReview>(`${this.baseUrl}/trail/${trailId}`, review);
  }

  public getMyTrailReview(trailId: number): Observable<TrailReview> {
    return this.http.get<TrailReview>(`${this.baseUrl}/trail/${trailId}/my-review`);
  }

  public deleteReview(reviewId: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/${reviewId}`);
  }

  public canReviewTrail(trailId: number): Observable<CanReviewResponse> {
    return this.http.get<CanReviewResponse>(`${this.baseUrl}/trail/${trailId}/can-review`);
  }

  public getStarArray(rating: number): boolean[] {
    return Array(5).fill(false).map((_, index) => index < rating);
  }

  public getDifficultyColor(difficulty?: string): string {
    switch (difficulty?.toLowerCase()) {
      case 'easy': return '#4CAF50';
      case 'moderate': return '#FFC107';
      case 'difficult': return '#FF9800';
      case 'very difficult': return '#F44336';
      case 'expert': return '#9C27B0';
      default: return '#9E9E9E';
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
}
