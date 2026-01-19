using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace WebFrontend.Pages;

public partial class Weather : ComponentBase
{
    [Inject] public HttpClient Http { get; set; } = default!;

    private WeatherForecast[]? forecasts;

    protected override async Task OnInitializedAsync()
    {
        forecasts = await Http.GetFromJsonAsync<WeatherForecast[]>("sample-data/weather.json");
    }

    public class WeatherForecast
    {
        public DateOnly Date { get; set; }

        public int TemperatureC { get; set; }

        public string? Summary { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
