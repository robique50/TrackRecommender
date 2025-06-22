import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface RecommendationSettings {
  count: number;
  includeWeather: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class RecommendationSettingsService {
  private readonly STORAGE_KEY = 'recommendationSettings';
  private readonly DEFAULT_SETTINGS: RecommendationSettings = {
    count: 10,
    includeWeather: true,
  };

  private settingsSubject = new BehaviorSubject<RecommendationSettings>(
    this.loadSettings()
  );
  public settings$: Observable<RecommendationSettings> =
    this.settingsSubject.asObservable();

  constructor() {}

  public getSettings(): RecommendationSettings {
    return this.settingsSubject.value;
  }

  public updateSettings(settings: Partial<RecommendationSettings>): void {
    const currentSettings = this.getSettings();
    const newSettings = { ...currentSettings, ...settings };

    if (newSettings.count < 1) newSettings.count = 1;
    if (newSettings.count > 50) newSettings.count = 50;

    try {
      localStorage.setItem(this.STORAGE_KEY, JSON.stringify(newSettings));
    } catch (error) {
      console.error('Failed to save settings to localStorage:', error);
    }

    this.settingsSubject.next(newSettings);
  }

  public resetSettings(): void {
    try {
      localStorage.removeItem(this.STORAGE_KEY);
    } catch (error) {
      console.error('Failed to remove settings from localStorage:', error);
    }
    this.settingsSubject.next(this.DEFAULT_SETTINGS);
  }

  private loadSettings(): RecommendationSettings {
    try {
      const saved = localStorage.getItem(this.STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (
          typeof parsed.count === 'number' &&
          typeof parsed.includeWeather === 'boolean'
        ) {
          return parsed;
        }
      }
    } catch (error) {
      console.error('Failed to load settings from localStorage:', error);
    }
    return this.DEFAULT_SETTINGS;
  }
}
