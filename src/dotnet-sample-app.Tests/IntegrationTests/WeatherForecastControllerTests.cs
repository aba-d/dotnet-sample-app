using System.Net.Http;
using System.Threading.Tasks;
using dotnet_sample_app.Controllers; // your controller namespace
using Microsoft.AspNetCore.Mvc.Testing; // for WebApplicationFactory
using Xunit;

namespace dotnet_sample_app.Tests.IntegrationTests
{
    public class WeatherForecastControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public WeatherForecastControllerTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Get_ReturnsSuccessStatusCode()
        {
            var response = await _client.GetAsync("/WeatherForecast");
            response.EnsureSuccessStatusCode();
        }
    }
}
