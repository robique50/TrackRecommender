import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { WeatherResponse } from '../../models/weather.models';

@Injectable({
  providedIn: 'root',
})
export class WeatherService {
  constructor(private http: HttpClient) {}

  getWeatherByCoordinates(
    latitude: number,
    longitude: number,
  ): Observable<WeatherResponse> {
    return this.http.get<WeatherResponse>(
      `/api/weather/coordinates?latitude=${latitude}&longitude=${longitude}`,
    );
  }

  getWeatherByLocation(query: string): Observable<WeatherResponse> {
    return this.http.get<WeatherResponse>(
      `/api/weather/location?query=${encodeURIComponent(query)}`,
    );
  }

  getDeterminedWeatherIconCode(weatherData: any): string {
    if (weatherData?.weather?.[0]?.icon) {
      return weatherData.weather[0].icon;
    }

    const clouds = weatherData?.clouds || 0;
    const temp = weatherData?.temp?.day || weatherData?.temp || 0;
    const humidity = weatherData?.humidity || 0;

    if (clouds > 80) {
      return '04d';
    } else if (clouds > 50) {
      return '03d';
    } else if (clouds > 20) {
      return '02d';
    } else {
      if (humidity > 80 && temp > 15) {
        return '10d';
      } else if (temp < 0) {
        return '13d';
      } else {
        return '01d';
      }
    }
  }

  getWeatherIconUrl(weatherData: any): string {
    const iconCode = this.getDeterminedWeatherIconCode(weatherData);
    return `https://openweathermap.org/img/wn/${iconCode}@2x.png`;
  }

  getDeterminedWeatherDescription(weatherData: any): string {
    if (weatherData?.weather?.[0]?.description) {
      return weatherData.weather[0].description;
    }

    const clouds = weatherData?.clouds || 0;
    const temp = weatherData?.temp?.day || weatherData?.temp || 0;
    const humidity = weatherData?.humidity || 0;

    if (clouds > 80) {
      return 'Overcast clouds';
    } else if (clouds > 50) {
      return 'Broken clouds';
    } else if (clouds > 20) {
      return 'Few clouds';
    } else {
      if (humidity > 80 && temp > 15) {
        return 'Light rain';
      } else if (temp < 0) {
        return 'Snow';
      } else {
        return 'Clear sky';
      }
    }
  }

  formatDay(timestamp: number): string {
    const date = new Date(timestamp * 1000);
    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);

    if (date.toDateString() === today.toDateString()) {
      return 'Today';
    } else if (date.toDateString() === tomorrow.toDateString()) {
      return 'Tomorrow';
    }

    return date.toLocaleDateString('en-US', { weekday: 'short' });
  }
}
