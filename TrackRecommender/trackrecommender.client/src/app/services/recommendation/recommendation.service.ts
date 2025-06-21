import { Injectable } from '@angular/core';
import {
  HttpClient,
  HttpErrorResponse,
  HttpParams,
} from '@angular/common/http';
import { Observable, BehaviorSubject, throwError } from 'rxjs';
import { tap, shareReplay, catchError } from 'rxjs/operators';
import {
  RecommendationResponse,
  TrailRecommendation,
} from '../../models/trail-recommendation.model';

@Injectable({
  providedIn: 'root',
})
export class RecommendationService {
  private readonly baseUrl = 'api/trailrecommendation';
  private recommendationsSubject = new BehaviorSubject<TrailRecommendation[]>(
    []
  );
  public recommendations$ = this.recommendationsSubject.asObservable();

  constructor(private http: HttpClient) {}

  public getRecommendations(
    count: number = 10,
    includeWeather: boolean = true
  ): Observable<RecommendationResponse> {
    const params = new HttpParams()
      .set('count', count.toString())
      .set('includeWeather', includeWeather.toString());

    return this.http.get<RecommendationResponse>(this.baseUrl, { params }).pipe(
      tap((response) => {
        console.log('API response received:', response);
        this.recommendationsSubject.next(response.recommendations);
      }),
      catchError((error: HttpErrorResponse) => {
        console.error('API error details:', error);

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
