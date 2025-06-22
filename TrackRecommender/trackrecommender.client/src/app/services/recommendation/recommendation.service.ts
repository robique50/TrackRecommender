import { Injectable } from '@angular/core';
import {
  HttpClient,
  HttpErrorResponse,
  HttpParams,
} from '@angular/common/http';
import { Observable, BehaviorSubject, throwError, of } from 'rxjs';
import { tap, shareReplay, catchError } from 'rxjs/operators';
import {
  RecommendationCache,
  RecommendationResponse,
  TrailRecommendation,
} from '../../models/trail-recommendation.model';

@Injectable({
  providedIn: 'root',
})
export class RecommendationService {
  private readonly baseUrl = 'api/trailrecommendation';
  private readonly CACHE_DURATION = 60 * 60 * 1000;

  private recommendationsSubject = new BehaviorSubject<TrailRecommendation[]>(
    []
  );
  public recommendations$ = this.recommendationsSubject.asObservable();
  private cache: RecommendationCache | null = null;
  private ongoingRequest: Observable<RecommendationResponse> | null = null;

  constructor(private http: HttpClient) {}

  public getRecommendations(
    count: number = 10,
    includeWeather: boolean = true,
    forceRefresh: boolean = false
  ): Observable<RecommendationResponse> {
    if (!forceRefresh && this.isCacheValid(count, includeWeather)) {
      return of(this.cache!.data);
    }

    if (
      !forceRefresh &&
      this.ongoingRequest &&
      this.cache?.params.count === count &&
      this.cache?.params.includeWeather === includeWeather
    ) {
      return this.ongoingRequest;
    }

    const params = new HttpParams()
      .set('count', count.toString())
      .set('includeWeather', includeWeather.toString());

    this.ongoingRequest = this.http
      .get<RecommendationResponse>(this.baseUrl, { params })
      .pipe(
        tap((response) => {
          this.cache = {
            data: response,
            timestamp: Date.now(),
            params: { count, includeWeather },
          };
          this.recommendationsSubject.next(response.recommendations);
          this.ongoingRequest = null;
        }),
        catchError((error: HttpErrorResponse) => {
          console.error('API error details:', error);
          this.ongoingRequest = null;

          if (error.error instanceof Error) {
            console.error('Client-side error:', error.error.message);
          } else if (
            typeof error.error === 'string' &&
            error.error.includes('<!doctype html>')
          ) {
            console.error(
              'Server returned HTML instead of JSON. Proxy configuration might be incorrect.'
            );
          }

          return throwError(
            () => new Error(`Error loading recommendations: ${error.message}`)
          );
        }),
        shareReplay(1)
      );

    return this.ongoingRequest;
  }

  private isCacheValid(count: number, includeWeather: boolean): boolean {
    if (!this.cache) return false;

    const now = Date.now();
    const isExpired = now - this.cache.timestamp > this.CACHE_DURATION;
    const paramsMatch =
      this.cache.params.count === count &&
      this.cache.params.includeWeather === includeWeather;

    return !isExpired && paramsMatch;
  }

  public invalidateCache(): void {
    this.cache = null;
    this.ongoingRequest = null;
  }

  public getCachedRecommendations(): TrailRecommendation[] | null {
    if (
      this.cache &&
      this.isCacheValid(
        this.cache.params.count,
        this.cache.params.includeWeather
      )
    ) {
      return this.cache.data.recommendations;
    }
    return null;
  }

  public preloadRecommendations(
    count: number = 10,
    includeWeather: boolean = true
  ): void {
    this.getRecommendations(count, includeWeather).subscribe({
      next: () => console.log('Recommendations preloaded'),
      error: (error) =>
        console.error('Error preloading recommendations:', error),
    });
  }

  public getScoreIcon(component: string): string {
    const icons: { [key: string]: string } = {
      difficulty: '🏔️',
      trailType: '🥾',
      distance: '📏',
      duration: '⏱️',
      rating: '⭐',
      weather: '🌤️',
    };
    return icons[component] || '📊';
  }

  public getScoreColor(score: number): string {
    if (score >= 80) return '#4CAF50';
    if (score >= 60) return '#8BC34A';
    if (score >= 40) return '#FFC107';
    if (score >= 20) return '#FF9800';
    return '#F44336';
  }
}
