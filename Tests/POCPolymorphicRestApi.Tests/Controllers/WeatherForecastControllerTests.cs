using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using POCPolymorphicRestApi.Controllers;
using Xunit;

namespace POCPolymorphicRestApi.Tests.Controllers;

public class WeatherForecastControllerTests
{
    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastControllerTests()
    {
        _logger = Substitute.For<ILogger<WeatherForecastController>>();
    }

    private WeatherForecastController CreateWeatherForecastController()
    {
        return new WeatherForecastController(_logger);
    }

    [Fact]
    public void Get_StateUnderTest_ExpectedBehavior()
    {
        // Arrange
        var weatherForecastController = CreateWeatherForecastController();

        // Act
        var result = weatherForecastController.Get();

        // Assert
        result.Should().NotBeNull();
    }
}
