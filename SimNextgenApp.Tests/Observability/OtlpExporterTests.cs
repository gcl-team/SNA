using SimNextgenApp.Observability.Exporters;

namespace SimNextgenApp.Tests.Observability;

public class OtlpExporterTests
{
    [Fact]
    public void ConfigureForBackend_GrafanaCloud_WithDefaultRegion_ReturnsCorrectConfiguration()
    {
        var apiKey = "12345:my-api-token";
        var config = OtlpExporterConfiguration.ConfigureForBackend(OtlpBackend.GrafanaCloud, apiKey);

        Assert.NotNull(config);
        Assert.Equal("https://otlp-gateway-prod-us-central-0.grafana.net/otlp", config.Endpoint.ToString());
        Assert.Contains("Authorization", config.Headers.Keys);
        Assert.StartsWith("Basic ", config.Headers["Authorization"]);
    }

    [Fact]
    public void ConfigureForBackend_GrafanaCloud_WithCustomRegion_ReturnsCorrectConfiguration()
    {
        var apiKey = "12345:my-api-token";
        var config = OtlpExporterConfiguration.ConfigureForBackend(OtlpBackend.GrafanaCloud, apiKey, region: "eu-west-0");

        Assert.NotNull(config);
        Assert.Equal("https://otlp-gateway-prod-eu-west-0.grafana.net/otlp", config.Endpoint.ToString());
        Assert.Contains("Authorization", config.Headers.Keys);
    }

    [Fact]
    public void ConfigureForBackend_Datadog_WithDefaultRegion_ReturnsCorrectConfiguration()
    {
        var apiKey = "my-datadog-api-key";
        var config = OtlpExporterConfiguration.ConfigureForBackend(OtlpBackend.Datadog, apiKey);

        Assert.NotNull(config);
        Assert.Equal("https://api.us1.datadoghq.com/api/v2/otlp", config.Endpoint.ToString());
        Assert.Contains("dd-api-key", config.Headers.Keys);
        Assert.Equal(apiKey, config.Headers["dd-api-key"]);
    }

    [Fact]
    public void ConfigureForBackend_Datadog_WithCustomRegion_ReturnsCorrectConfiguration()
    {
        var apiKey = "my-datadog-api-key";
        var config = OtlpExporterConfiguration.ConfigureForBackend(OtlpBackend.Datadog, apiKey, region: "eu1");

        Assert.NotNull(config);
        Assert.Equal("https://api.eu1.datadoghq.com/api/v2/otlp", config.Endpoint.ToString());
        Assert.Equal(apiKey, config.Headers["dd-api-key"]);
    }

    [Fact]
    public void ConfigureForBackend_Honeycomb_WithDefaultRegion_ReturnsCorrectConfiguration()
    {
        var apiKey = "my-honeycomb-api-key";
        var config = OtlpExporterConfiguration.ConfigureForBackend(OtlpBackend.Honeycomb, apiKey);

        Assert.NotNull(config);
        Assert.Equal("https://api.honeycomb.io/v1/traces", config.Endpoint.ToString());
        Assert.Contains("x-honeycomb-team", config.Headers.Keys);
        Assert.Equal(apiKey, config.Headers["x-honeycomb-team"]);
    }

    [Fact]
    public void ConfigureForBackend_Honeycomb_WithCustomRegion_ReturnsCorrectConfiguration()
    {
        var apiKey = "my-honeycomb-api-key";
        var config = OtlpExporterConfiguration.ConfigureForBackend(OtlpBackend.Honeycomb, apiKey, region: "eu1");

        Assert.NotNull(config);
        Assert.Equal("https://api.eu1.honeycomb.io/v1/traces", config.Endpoint.ToString());
        Assert.Contains("x-honeycomb-team", config.Headers.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigureForBackend_WithNullOrEmptyApiKey_ThrowsArgumentNullException(string? apiKey)
    {
        Assert.Throws<ArgumentNullException>(() =>
            OtlpExporterConfiguration.ConfigureForBackend(OtlpBackend.GrafanaCloud, apiKey!));
    }

    [Fact]
    public void CreateGrafanaCloudConfig_WithValidApiKey_ReturnsConfiguration()
    {
        var apiKey = "instance123:token456";
        var config = OtlpExporterConfiguration.CreateGrafanaCloudConfig(apiKey);

        Assert.NotNull(config);
        Assert.Equal("https://otlp-gateway-prod-us-central-0.grafana.net/otlp", config.Endpoint.ToString());
        Assert.Contains("Authorization", config.Headers.Keys);

        // Verify Base64 encoding
        var authHeader = config.Headers["Authorization"];
        Assert.StartsWith("Basic ", authHeader);
        var encodedValue = authHeader.Substring("Basic ".Length);
        var decodedBytes = Convert.FromBase64String(encodedValue);
        var decodedString = System.Text.Encoding.UTF8.GetString(decodedBytes);
        Assert.Equal(apiKey, decodedString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateGrafanaCloudConfig_WithInvalidApiKey_ThrowsArgumentNullException(string? apiKey)
    {
        Assert.Throws<ArgumentNullException>(() =>
            OtlpExporterConfiguration.CreateGrafanaCloudConfig(apiKey!));
    }

    [Theory]
    [InlineData("us-central-0", "https://otlp-gateway-prod-us-central-0.grafana.net/otlp")]
    [InlineData("eu-west-0", "https://otlp-gateway-prod-eu-west-0.grafana.net/otlp")]
    [InlineData("ap-southeast-0", "https://otlp-gateway-prod-ap-southeast-0.grafana.net/otlp")]
    [InlineData("au-southeast-0", "https://otlp-gateway-prod-au-southeast-0.grafana.net/otlp")]
    public void CreateGrafanaCloudConfig_WithDifferentRegions_ReturnsCorrectEndpoint(string region, string expectedEndpoint)
    {
        var apiKey = "instance123:token456";
        var config = OtlpExporterConfiguration.CreateGrafanaCloudConfig(apiKey, region);

        Assert.Equal(expectedEndpoint, config.Endpoint.ToString());
    }

    [Fact]
    public void CreateDatadogConfig_WithDefaultRegion_ReturnsConfiguration()
    {
        var apiKey = "dd-api-key-123";
        var config = OtlpExporterConfiguration.CreateDatadogConfig(apiKey);

        Assert.NotNull(config);
        Assert.Equal("https://api.us1.datadoghq.com/api/v2/otlp", config.Endpoint.ToString());
        Assert.Equal(apiKey, config.Headers["dd-api-key"]);
    }

    [Theory]
    [InlineData("us1", "https://api.us1.datadoghq.com/api/v2/otlp")]
    [InlineData("us3", "https://api.us3.datadoghq.com/api/v2/otlp")]
    [InlineData("us5", "https://api.us5.datadoghq.com/api/v2/otlp")]
    [InlineData("eu1", "https://api.eu1.datadoghq.com/api/v2/otlp")]
    [InlineData("ap1", "https://api.ap1.datadoghq.com/api/v2/otlp")]
    [InlineData("gov", "https://api.gov.datadoghq.com/api/v2/otlp")]
    public void CreateDatadogConfig_WithDifferentRegions_ReturnsCorrectEndpoint(string region, string expectedEndpoint)
    {
        var apiKey = "dd-api-key-123";
        var config = OtlpExporterConfiguration.CreateDatadogConfig(apiKey, region);

        Assert.Equal(expectedEndpoint, config.Endpoint.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDatadogConfig_WithInvalidApiKey_ThrowsArgumentNullException(string? apiKey)
    {
        Assert.Throws<ArgumentNullException>(() =>
            OtlpExporterConfiguration.CreateDatadogConfig(apiKey!));
    }

    [Fact]
    public void CreateHoneycombConfig_WithValidApiKey_ReturnsConfiguration()
    {
        var apiKey = "honeycomb-api-key-123";
        var config = OtlpExporterConfiguration.CreateHoneycombConfig(apiKey);

        Assert.NotNull(config);
        Assert.Equal("https://api.honeycomb.io/v1/traces", config.Endpoint.ToString());
        Assert.Equal(apiKey, config.Headers["x-honeycomb-team"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateHoneycombConfig_WithInvalidApiKey_ThrowsArgumentNullException(string? apiKey)
    {
        Assert.Throws<ArgumentNullException>(() =>
            OtlpExporterConfiguration.CreateHoneycombConfig(apiKey!));
    }

    [Theory]
    [InlineData(null, "https://api.honeycomb.io/v1/traces")]
    [InlineData("us", "https://api.honeycomb.io/v1/traces")]
    [InlineData("US", "https://api.honeycomb.io/v1/traces")]
    [InlineData("eu1", "https://api.eu1.honeycomb.io/v1/traces")]
    [InlineData("EU1", "https://api.eu1.honeycomb.io/v1/traces")]
    public void CreateHoneycombConfig_WithDifferentRegions_ReturnsCorrectEndpoint(string? region, string expectedEndpoint)
    {
        var apiKey = "honeycomb-api-key-123";
        var config = OtlpExporterConfiguration.CreateHoneycombConfig(apiKey, region);

        Assert.Equal(expectedEndpoint, config.Endpoint.ToString());
    }

    [Fact]
    public void OtlpBackend_EnumValues_AreCorrect()
    {
        Assert.Equal(0, (int)OtlpBackend.GrafanaCloud);
        Assert.Equal(1, (int)OtlpBackend.Datadog);
        Assert.Equal(2, (int)OtlpBackend.Honeycomb);
    }
}
