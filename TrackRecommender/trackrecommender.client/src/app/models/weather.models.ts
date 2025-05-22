export interface WeatherCondition {
  id: number;
  main: string;
  description: string;
  icon: string;
}

export interface WeatherTemp {
  day: number;
  min: number;
  max: number;
  night: number;
}

export interface WeatherDaily {
  dt: number;
  temp: WeatherTemp;
  humidity: number;
  windSpeed: number;
  weather: WeatherCondition[];
}

export interface WeatherCurrent {
  temp: number;
  feelsLike: number;
  humidity: number;
  windSpeed: number;
  clouds: number;
  weather: WeatherCondition[];
}

export interface WeatherLocation {
  name: string;
  country: string;
  lat: number;
  lon: number;
}

export interface WeatherResponse {
  location: WeatherLocation;
  current: WeatherCurrent;
  daily: WeatherDaily[];
}
