import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap, catchError } from 'rxjs/operators';
import {
  UserPreferences,
  PreferenceOptions,
} from '../../models/user-preferences.model';

@Injectable({
  providedIn: 'root',
})
export class UserPreferencesService {
  private preferencesSubject = new BehaviorSubject<UserPreferences | null>(
    null
  );
  public preferences$ = this.preferencesSubject.asObservable();

  constructor(private http: HttpClient) {}

  public getUserPreferences(): Observable<UserPreferences> {
    return this.http.get<UserPreferences>('/api/userpreferences').pipe(
      tap((preferences) => this.preferencesSubject.next(preferences)),
      catchError((error) => {
        console.error('Error loading user preferences:', error);
        throw error;
      })
    );
  }

  public saveUserPreferences(preferences: UserPreferences): Observable<any> {
    return this.http.post('/api/userpreferences', preferences).pipe(
      tap(() => this.preferencesSubject.next(preferences)),
      catchError((error) => {
        console.error('Error saving user preferences:', error);
        throw error;
      })
    );
  }

  public getPreferenceOptions(): Observable<PreferenceOptions> {
    return this.http
      .get<PreferenceOptions>('/api/userpreferences/options')
      .pipe(
        catchError((error) => {
          console.error('Error loading preference options:', error);
          throw error;
        })
      );
  }

  public getCurrentPreferences(): UserPreferences | null {
    return this.preferencesSubject.value;
  }

  public resetUserPreferences(): Observable<any> {
    return this.http.delete('/api/userpreferences');
  }
}
