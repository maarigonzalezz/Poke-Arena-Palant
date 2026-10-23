namespace PokeArena.Api;

/// <summary>Modelo del ejemplo meteorológico de ASP.NET; no forma parte del catálogo.</summary>
public class WeatherForecast
{
    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}
