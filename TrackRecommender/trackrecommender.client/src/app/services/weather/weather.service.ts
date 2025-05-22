import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { WeatherResponse } from '../../models/weather.models';


@Injectable({
  providedIn: 'root'
})
export class WeatherService {
  constructor(private http: HttpClient) { }

  getWeatherByCoordinates(latitude: number, longitude: number): Observable<WeatherResponse> {
    return this.http.get<WeatherResponse>(`/api/weather/coordinates?latitude=${latitude}&longitude=${longitude}`);
  }

  getWeatherByLocation(query: string): Observable<WeatherResponse> {
    return this.http.get<WeatherResponse>(`/api/weather/location?query=${encodeURIComponent(query)}`);
  }

  getWeatherIconUrl(iconCode: string): string {
    return `https://openweathermap.org/img/wn/${iconCode}@2x.png`;
  }

  formatDay(timestamp: number): string {
    const date = new Date(timestamp * 1000);
    return date.toLocaleDateString('en-US', { weekday: 'short' });
  }
}
