import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject, of, throwError } from 'rxjs';
import { catchError, tap, shareReplay } from 'rxjs/operators';
import { Trail, TrailFilters } from '../../models/trail.model';
import { TrailCache } from '../../helpers/trail-cache';

@Injectable({
  providedIn: 'root',
})
export class TrailService {
  private readonly CACHE_DURATION = 5 * 60 * 1000;
  private trailsCache = new Map<string, TrailCache<Trail>>();
  private ongoingRequests = new Map<string, Observable<Trail[]>>();

  private trailsUpdated$ = new BehaviorSubject<boolean>(false);
  public trailsUpdateNotification$ = this.trailsUpdated$.asObservable();

  constructor(private http: HttpClient) {}

  getTrails(
    filters?: TrailFilters,
    forceRefresh: boolean = false
  ): Observable<Trail[]> {
    const cacheKey = this.getCacheKey(filters);

    if (!forceRefresh && this.ongoingRequests.has(cacheKey)) {
      return this.ongoingRequests.get(cacheKey)!;
    }

    if (!forceRefresh && this.isCacheValid(cacheKey)) {
      return of(this.trailsCache.get(cacheKey)!.data);
    }

    let params = new HttpParams();
    if (filters) {
      Object.keys(filters).forEach((key) => {
        const filterValue = filters[key as keyof TrailFilters];
        if (filterValue !== undefined && filterValue !== null) {
          if (Array.isArray(filterValue)) {
            filterValue.forEach((value: any) => {
              params = params.append(key, value.toString());
            });
          } else {
            params = params.append(key, filterValue.toString());
          }
        }
      });
    }

    const request$ = this.http
      .get<Trail[]>('/api/mapdata/trails', { params })
      .pipe(
        tap((data) => {
          this.trailsCache.set(cacheKey, {
            data,
            timestamp: Date.now(),
            filters: cacheKey,
          });

          this.ongoingRequests.delete(cacheKey);
          this.trailsUpdated$.next(true);
        }),
        catchError((error) => {
          this.ongoingRequests.delete(cacheKey);
          console.error('Error loading trails:', error);
          return throwError(() => error);
        }),
        shareReplay(1)
      );

    this.ongoingRequests.set(cacheKey, request$);
    return request$;
  }

  getTrailById(id: number, forceRefresh: boolean = false): Observable<Trail> {
    const cacheKey = `trail_${id}`;

    if (!forceRefresh) {
      for (const [key, cache] of this.trailsCache.entries()) {
        const trail = cache.data.find((t) => t.id === id);
        if (trail && this.isCacheValid(key)) {
          return of(trail);
        }
      }
    }

    return this.http.get<Trail>(`/api/mapdata/trails/${id}`).pipe(
      tap((trail) => {
        // Optionally cache individual trail
      }),
      catchError((error) => {
        console.error('Error loading trail:', error);
        return throwError(() => error);
      })
    );
  }

  getRecommendedTrails(forceRefresh: boolean = false): Observable<Trail[]> {
    const cacheKey = 'recommended_trails';

    if (!forceRefresh && this.isCacheValid(cacheKey)) {
      return of(this.trailsCache.get(cacheKey)!.data);
    }

    return this.http.get<Trail[]>('/api/mapdata/recommended').pipe(
      tap((data) => {
        this.trailsCache.set(cacheKey, {
          data,
          timestamp: Date.now(),
          filters: cacheKey,
        });
      }),
      catchError((error) => {
        console.error('Error loading recommended trails:', error);
        return throwError(() => error);
      })
    );
  }

  invalidateCache(specificKey?: string): void {
    if (specificKey) {
      this.trailsCache.delete(specificKey);
    } else {
      this.trailsCache.clear();
    }
    this.ongoingRequests.clear();
  }

  updateTrailInCache(trailId: number, updates: Partial<Trail>): void {
    for (const [key, cache] of this.trailsCache.entries()) {
      const trailIndex = cache.data.findIndex((t) => t.id === trailId);
      if (trailIndex !== -1) {
        cache.data[trailIndex] = { ...cache.data[trailIndex], ...updates };
      }
    }
    this.trailsUpdated$.next(true);
  }

  getCacheStats(): { size: number; entries: string[] } {
    return {
      size: this.trailsCache.size,
      entries: Array.from(this.trailsCache.keys()),
    };
  }

  private getCacheKey(filters?: TrailFilters): string {
    if (!filters) return 'all_trails';

    const sortedKeys = Object.keys(filters).sort();
    const keyParts: string[] = [];

    sortedKeys.forEach((key) => {
      const value = filters[key as keyof TrailFilters];
      if (value !== undefined && value !== null) {
        if (Array.isArray(value)) {
          keyParts.push(`${key}:${value.sort().join(',')}`);
        } else {
          keyParts.push(`${key}:${value}`);
        }
      }
    });

    return keyParts.length > 0 ? keyParts.join('|') : 'all_trails';
  }

  private isCacheValid(key: string): boolean {
    const cache = this.trailsCache.get(key);
    if (!cache) return false;

    const now = Date.now();
    const isValid = now - cache.timestamp < this.CACHE_DURATION;

    if (!isValid) {
      this.trailsCache.delete(key);
    }

    return isValid;
  }

  public preloadTrails(): void {
    this.getTrails().subscribe({
      error: (error) => console.error('Error preloading trails:', error),
    });
  }
}
