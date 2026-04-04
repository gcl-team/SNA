using SimNextgenApp.Observability.Exporters;

namespace SimNextgenApp.Tests.Observability;

public class PrometheusExporterTests
{
    [Theory]
    [InlineData("sna.server.utilization", "sna_server_utilization")]
    [InlineData("sna.queue.occupancy", "sna_queue_occupancy")]
    [InlineData("sna.loads.completed", "sna_loads_completed")]
    [InlineData("metric-with-hyphens", "metric_with_hyphens")]
    [InlineData("metric.with.dots", "metric_with_dots")]
    [InlineData("mixed-metric.name", "mixed_metric_name")]
    public void ConvertToPrometheusName_ValidNames_ConvertsCorrectly(string input, string expected)
    {
        var result = PrometheusExporter.ConvertToPrometheusName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertToPrometheusName_StartsWithDigit_PrependsUnderscore()
    {
        var result = PrometheusExporter.ConvertToPrometheusName("123metric");
        Assert.Equal("_123metric", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertToPrometheusName_NullOrEmpty_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => PrometheusExporter.ConvertToPrometheusName(input!));
    }

    [Theory]
    [InlineData("valid_metric_name", true)]
    [InlineData("metric:with:colons", true)]
    [InlineData("metric_123", true)]
    [InlineData("_metric", true)]
    [InlineData(":metric", true)]
    [InlineData("UPPERCASE_METRIC", true)]
    [InlineData("123invalid", false)]  // starts with digit
    [InlineData("metric-with-hyphen", false)]  // contains hyphen
    [InlineData("metric.with.dot", false)]  // contains dot
    [InlineData("metric with space", false)]  // contains space
    [InlineData("", false)]  // empty
    public void ValidatePrometheusName_VariousInputs_ReturnsExpectedResult(string input, bool expected)
    {
        var result = PrometheusExporter.ValidatePrometheusName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidatePrometheusName_NullInput_ReturnsFalse()
    {
        var result = PrometheusExporter.ValidatePrometheusName(null!);
        Assert.False(result);
    }

    [Fact]
    public void GenerateScrapeConfig_WithDefaults_ReturnsValidYaml()
    {
        var config = PrometheusExporter.GenerateScrapeConfig();

        Assert.Contains("scrape_configs:", config);
        Assert.Contains("job_name: 'sna-simulation'", config);
        Assert.Contains("scrape_interval: 10s", config);
        Assert.Contains("static_configs:", config);
        Assert.Contains("targets: ['localhost:9090']", config);
    }

    [Fact]
    public void GenerateScrapeConfig_WithCustomParameters_ReturnsValidYaml()
    {
        var config = PrometheusExporter.GenerateScrapeConfig(
            jobName: "my-simulation",
            scrapeInterval: "5s",
            targetHost: "192.168.1.100",
            targetPort: 8080);

        Assert.Contains("job_name: 'my-simulation'", config);
        Assert.Contains("scrape_interval: 5s", config);
        Assert.Contains("targets: ['192.168.1.100:8080']", config);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateScrapeConfig_WithNullOrEmptyJobName_ThrowsArgumentException(string? jobName)
    {
        Assert.Throws<ArgumentException>(() =>
            PrometheusExporter.GenerateScrapeConfig(jobName: jobName!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateScrapeConfig_WithNullOrEmptyScrapeInterval_ThrowsArgumentException(string? scrapeInterval)
    {
        Assert.Throws<ArgumentException>(() =>
            PrometheusExporter.GenerateScrapeConfig(scrapeInterval: scrapeInterval!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateScrapeConfig_WithNullOrEmptyTargetHost_ThrowsArgumentException(string? targetHost)
    {
        Assert.Throws<ArgumentException>(() =>
            PrometheusExporter.GenerateScrapeConfig(targetHost: targetHost!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void GenerateScrapeConfig_WithInvalidPort_ThrowsArgumentOutOfRangeException(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrometheusExporter.GenerateScrapeConfig(targetPort: port));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(9090)]
    [InlineData(65535)]
    public void GenerateScrapeConfig_WithValidPorts_Succeeds(int port)
    {
        var config = PrometheusExporter.GenerateScrapeConfig(targetPort: port);
        Assert.Contains($"targets: ['localhost:{port}']", config);
    }

    [Fact]
    public void GenerateScrapeConfig_OutputFormat_IsWellFormed()
    {
        var config = PrometheusExporter.GenerateScrapeConfig(
            jobName: "test-job",
            scrapeInterval: "15s",
            targetHost: "example.com",
            targetPort: 9091);

        var lines = config.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("scrape_configs:", lines[0]);
        Assert.Equal("  - job_name: 'test-job'", lines[1]);
        Assert.Equal("    scrape_interval: 15s", lines[2]);
        Assert.Equal("    static_configs:", lines[3]);
        Assert.Equal("      - targets: ['example.com:9091']", lines[4]);
    }

    [Fact]
    public void ConvertToPrometheusName_PreservesColons()
    {
        var result = PrometheusExporter.ConvertToPrometheusName("metric:with:colons");
        Assert.Equal("metric:with:colons", result);
    }

    [Fact]
    public void ConvertToPrometheusName_PreservesUnderscores()
    {
        var result = PrometheusExporter.ConvertToPrometheusName("metric_with_underscores");
        Assert.Equal("metric_with_underscores", result);
    }

    [Fact]
    public void ValidatePrometheusName_ComplexValidName_ReturnsTrue()
    {
        var result = PrometheusExporter.ValidatePrometheusName("http_requests_total:rate5m");
        Assert.True(result);
    }

    [Fact]
    public void ValidatePrometheusName_NameWithOnlyUnderscores_ReturnsTrue()
    {
        var result = PrometheusExporter.ValidatePrometheusName("___");
        Assert.True(result);
    }

    [Fact]
    public void ValidatePrometheusName_SingleCharacter_ReturnsTrue()
    {
        Assert.True(PrometheusExporter.ValidatePrometheusName("a"));
        Assert.True(PrometheusExporter.ValidatePrometheusName("_"));
        Assert.True(PrometheusExporter.ValidatePrometheusName(":"));
    }

    [Theory]
    [InlineData("metric with spaces", "metric_with_spaces")]
    [InlineData("metric/with/slashes", "metric_with_slashes")]
    [InlineData("metric@special#chars", "metric_special_chars")]
    [InlineData("metric$value%percent", "metric_value_percent")]
    [InlineData("metric(with)parens", "metric_with_parens")]
    [InlineData("metric[with]brackets", "metric_with_brackets")]
    [InlineData("metric{with}braces", "metric_with_braces")]
    [InlineData("metric!exclamation?question", "metric_exclamation_question")]
    public void ConvertToPrometheusName_WithInvalidCharacters_ReplacesWithUnderscores(string input, string expected)
    {
        var result = PrometheusExporter.ConvertToPrometheusName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("sna.server.utilization")]
    [InlineData("metric-with-hyphens")]
    [InlineData("metric with spaces")]
    [InlineData("metric/with/slashes")]
    [InlineData("123metric")]
    [InlineData("metric@#$%^&*()")]
    [InlineData("my:metric:name")]
    [InlineData("métric.test")]  // Unicode letter
    [InlineData("测试.metric")]  // Chinese characters
    [InlineData("метрика.test")]  // Cyrillic characters
    [InlineData("café.latency")]  // Accented character
    public void ConvertToPrometheusName_AlwaysReturnsValidPrometheusName(string input)
    {
        var result = PrometheusExporter.ConvertToPrometheusName(input);
        Assert.True(PrometheusExporter.ValidatePrometheusName(result),
            $"ConvertToPrometheusName returned '{result}' which failed Prometheus validation for input '{input}'");
    }

    [Theory]
    [InlineData("métric.test", "m_tric_test")]  // é replaced with _
    [InlineData("café.latency", "caf__latency")]  // é replaced with _
    [InlineData("测试.metric", "___metric")]  // Chinese chars replaced with _
    [InlineData("метрика", "_______")]  // Cyrillic chars all replaced with _ (7 chars)
    [InlineData("naïve", "na_ve")]  // ï replaced with _
    public void ConvertToPrometheusName_WithUnicodeCharacters_ReplacesWithUnderscores(string input, string expected)
    {
        var result = PrometheusExporter.ConvertToPrometheusName(input);
        Assert.Equal(expected, result);
    }
}
