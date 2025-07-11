import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { WeatherResponse } from '../../models/weather.model';
import { WeatherService } from '../../services/weather/weather.service';

@Component({
  selector: 'app-weather-widget',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './weather-widget.component.html',
  styleUrl: './weather-widget.component.scss',
})
export class WeatherWidgetComponent implements OnInit {
  protected weatherData: WeatherResponse | null = null;
  protected isLoading = false;
  protected error: string | null = null;
  protected locationQuery = '';
  protected currentLocationEnabled = false;

  constructor(private weatherService: WeatherService) {}

  ngOnInit(): void {
    if (navigator.geolocation) {
      this.currentLocationEnabled = true;
    }
  }

  protected getLocationWeather(): void {
    if (!navigator.geolocation) {
      this.error = 'Geolocation is not supported by your browser';
      return;
    }

    this.isLoading = true;
    this.error = null;

    navigator.geolocation.getCurrentPosition(
      (position) => {
        this.weatherService
          .getWeatherByCoordinates(
            position.coords.latitude,
            position.coords.longitude
          )
          .subscribe({
            next: (data) => {
              this.weatherData = data;
              this.isLoading = false;
            },
            error: (err) => {
              console.error('Error fetching weather:', err);
              this.error =
                'Failed to get weather data. Please try again later.';
              this.isLoading = false;
            },
          });
      },
      (err) => {
        console.error('Geolocation error:', err);
        this.error =
          'Could not get your location. Please make sure location services are enabled.';
        this.isLoading = false;
      }
    );
  }

  protected searchLocationWeather(): void {
    if (!this.locationQuery.trim()) {
      return;
    }

    this.isLoading = true;
    this.error = null;

    this.weatherService.getWeatherByLocation(this.locationQuery).subscribe({
      next: (data) => {
        this.weatherData = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error fetching weather:', err);
        if (err.status === 404) {
          this.error = `Location "${this.locationQuery}" not found. Please try another search.`;
        } else {
          this.error = 'Failed to get weather data. Please try again later.';
        }
        this.isLoading = false;
      },
    });
  }

  protected getWeatherIconUrl(): string {
    if (!this.weatherData || !this.weatherData.current) {
      return '';
    }

    return this.weatherService.getWeatherIconUrl(this.weatherData.current);
  }

  protected getWeatherDescription(): string {
    if (!this.weatherData || !this.weatherData.current) {
      return '';
    }

    return this.weatherService.getDeterminedWeatherDescription(
      this.weatherData.current
    );
  }

  protected getDailyWeatherIconUrl(day: any): string {
    if (!day) {
      return '';
    }

    return this.weatherService.getWeatherIconUrl(day);
  }

  protected getDailyWeatherDescription(day: any): string {
    if (!day) {
      return '';
    }

    return this.weatherService.getDeterminedWeatherDescription(day);
  }

  protected formatDay(timestamp: number): string {
    return this.weatherService.formatDay(timestamp);
  }
}
