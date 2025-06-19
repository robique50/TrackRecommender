import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { WeatherResponse } from '../../models/weather.model';

@Injectable({
  providedIn: 'root',
})
export class WeatherService {
  constructor(private http: HttpClient) {}

  public getWeatherByCoordinates(
    latitude: number,
    longitude: number
  ): Observable<WeatherResponse> {
    return this.http.get<WeatherResponse>(
      `/api/weather/coordinates?latitude=${latitude}&longitude=${longitude}`
    );
  }

  public getWeatherByLocation(query: string): Observable<WeatherResponse> {
    return this.http.get<WeatherResponse>(
      `/api/weather/location?query=${encodeURIComponent(query)}`
    );
  }

  public getDeterminedWeatherIconCode(weatherData: any): string {
    if (
      weatherData?.weather &&
      Array.isArray(weatherData.weather) &&
      weatherData.weather.length > 0
    ) {
      const icon = weatherData.weather[0]?.icon;
      if (icon && icon.trim() !== '') {
        return icon;
      }
    }

    const clouds = weatherData?.clouds || 0;
    const temp = weatherData?.temp?.day || weatherData?.temp || 0;
    const humidity = weatherData?.humidity || 0;
    const windSpeed = weatherData?.windSpeed || 0;

    if (humidity > 85 && temp > 10) {
      return '09d';
    } else if (humidity > 80 && temp > 15) {
      return '10d';
    } else if (temp < -5) {
      return '13d';
    } else if (temp < 0) {
      return '13d';
    } else if (windSpeed > 15 && clouds > 70) {
      return '50d';
    } else if (clouds > 85) {
      return '04d';
    } else if (clouds > 50) {
      return '03d';
    } else if (clouds > 20) {
      return '02d';
    } else {
      return '01d';
    }
  }

  public getWeatherIconUrl(weatherData: any): string {
    const iconCode = this.getDeterminedWeatherIconCode(weatherData);
    return `https://openweathermap.org/img/wn/${iconCode}@2x.png`;
  }

  public getDeterminedWeatherDescription(weatherData: any): string {
    if (
      weatherData?.weather &&
      Array.isArray(weatherData.weather) &&
      weatherData.weather.length > 0
    ) {
      const description = weatherData.weather[0]?.description;
      if (description && description.trim() !== '') {
        return description;
      }
    }

    const clouds = weatherData?.clouds || 0;
    const temp = weatherData?.temp?.day || weatherData?.temp || 0;
    const humidity = weatherData?.humidity || 0;
    const windSpeed = weatherData?.windSpeed || 0;

    if (humidity > 85 && temp > 10) {
      return 'Shower rain';
    } else if (humidity > 80 && temp > 15) {
      return 'Light rain';
    } else if (temp < -5) {
      return 'Heavy snow';
    } else if (temp < 0) {
      return 'Light snow';
    } else if (windSpeed > 15 && clouds > 70) {
      return 'Mist';
    } else if (clouds > 85) {
      return 'Overcast clouds';
    } else if (clouds > 50) {
      return 'Broken clouds';
    } else if (clouds > 20) {
      return 'Few clouds';
    } else {
      return 'Clear sky';
    }
  }

  public formatDay(timestamp: number): string {
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
