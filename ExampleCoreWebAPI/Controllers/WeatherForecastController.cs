using Microsoft.AspNetCore.Mvc;
using Shared;
using Data;
using Microsoft.EntityFrameworkCore;

namespace ExampleCoreWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly MainDataContext _dataContext;
        public WeatherForecastController(ILogger<WeatherForecastController> logger, MainDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        [HttpGet(Name = "GetWeatherForecasts")]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            return await _dataContext.Forecasts.ToListAsync();
        }

        [HttpPost(Name = "CreateWeatherForecast")]
        public async Task Create(WeatherForecast forecast)
        {
            await _dataContext.Forecasts.AddAsync(forecast);
            await _dataContext.SaveChangesAsync();
        }
    }
}