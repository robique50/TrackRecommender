import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';
import { UserPreferences, Region } from '../../models/user-preferences.model';

@Injectable({
  providedIn: 'root',
})
export class UserPreferencesService {
  private preferencesSubject = new BehaviorSubject<UserPreferences | null>(
    null
  );
  public preferences$ = this.preferencesSubject.asObservable();

  constructor(private http: HttpClient) {}

  getUserPreferences(): Observable<UserPreferences> {
    return this.http
      .get<UserPreferences>('/api/user/preferences')
      .pipe(tap((preferences) => this.preferencesSubject.next(preferences)));
  }

  saveUserPreferences(preferences: UserPreferences): Observable<any> {
    return this.http
      .post('/api/user/preferences', preferences)
      .pipe(tap(() => this.preferencesSubject.next(preferences)));
  }

  getAllRegions(): Observable<Region[]> {
    return this.http.get<Region[]>('/api/regions');
  }

  clearPreferencesCache(): void {
    this.preferencesSubject.next(null);
  }
}
