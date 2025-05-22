namespace TrackRecommender.Server.Models.DTOs
{
    public class WeatherResponseDto
    {
        public WeatherLocationDto Location { get; set; } = new WeatherLocationDto();
        public WeatherCurrentDto Current { get; set; } = new WeatherCurrentDto();
        public List<WeatherDailyDto> Daily { get; set; } = new List<WeatherDailyDto>();
    }

    public class WeatherLocationDto
    {
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class WeatherCurrentDto
    {
        public double Temp { get; set; }
        public double FeelsLike { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public int Clouds { get; set; }
        public List<WeatherConditionDto> Weather { get; set; } = new List<WeatherConditionDto>();
    }

    public class WeatherDailyDto
    {
        public long Dt { get; set; }
        public WeatherTempDto Temp { get; set; } = new WeatherTempDto();
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public List<WeatherConditionDto> Weather { get; set; } = new List<WeatherConditionDto>();
    }

    public class WeatherTempDto
    {
        public double Day { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Night { get; set; }
    }

    public class WeatherConditionDto
    {
        public int Id { get; set; }
        public string Main { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}