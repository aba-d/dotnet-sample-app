using System;
using System.Linq;  // << Add this
using Xunit;
using dotnet_sample_app.Services;

namespace dotnet_sample_app.Tests.UnitTests;

public class WeatherServiceTests
{
    [Fact]
    public void GetForecast_ShouldReturn5Items()
    {
        var service = new WeatherService();
        var result = service.GetForecast();

        Assert.Equal(5, result.Count());
    }
}
