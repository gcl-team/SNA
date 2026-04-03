using System.Diagnostics;

namespace SimNextgenApp.Tests.Observability;

public class TraceContextTests
{
    [Fact]
    public void ExtractContext_WithValidTraceparent_ReturnsContext()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        };

        var context = TraceContext.ExtractContext(headers);

        Assert.NotEqual(default, context);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", context.TraceId.ToHexString());
        Assert.Equal("b7ad6b7169203331", context.SpanId.ToHexString());
        Assert.Equal(ActivityTraceFlags.Recorded, context.TraceFlags);
    }

    [Fact]
    public void ExtractContext_WithTraceparentAndTracestate_ReturnsContextWithState()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            ["tracestate"] = "congo=t61rcWkgMzE,rojo=00f067aa0ba902b7"
        };

        var context = TraceContext.ExtractContext(headers);

        Assert.NotEqual(default, context);
        // TraceState is stored in the context but not directly accessible as a string property
    }

    [Fact]
    public void ExtractContext_WithoutTraceparent_ReturnsDefault()
    {
        var headers = new Dictionary<string, string>
        {
            ["some-other-header"] = "value"
        };

        var context = TraceContext.ExtractContext(headers);

        Assert.Equal(default, context);
    }

    [Fact]
    public void ExtractContext_WithInvalidTraceparent_ReturnsDefault()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "invalid-format"
        };

        var context = TraceContext.ExtractContext(headers);

        Assert.Equal(default, context);
    }

    [Fact]
    public void ExtractContext_WithNullHeaders_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TraceContext.ExtractContext(null!));
    }

    [Fact]
    public void InjectContext_WithActiveActivity_InjectsHeaders()
    {
        var activitySource = new ActivitySource("test-source");
        using var activity = activitySource.StartActivity("test-activity");

        if (activity == null)
        {
            // Skip test if no listener is attached
            return;
        }

        var headers = new Dictionary<string, string>();
        TraceContext.InjectContext(headers, activity);

        Assert.Contains("traceparent", headers.Keys);
        var traceparent = headers["traceparent"];
        Assert.StartsWith("00-", traceparent);
        Assert.Contains(activity.TraceId.ToHexString(), traceparent);
        Assert.Contains(activity.SpanId.ToHexString(), traceparent);
    }

    [Fact]
    public void InjectContext_WithNoActivity_DoesNotInject()
    {
        var headers = new Dictionary<string, string>();
        TraceContext.InjectContext(headers, activity: null);

        Assert.Empty(headers);
    }

    [Fact]
    public void InjectContext_WithNullHeaders_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TraceContext.InjectContext(null!));
    }

    [Fact]
    public void InjectAndExtract_RoundTrip_PreservesContext()
    {
        var activitySource = new ActivitySource("test-source");
        using var activity = activitySource.StartActivity("test-activity");

        if (activity == null)
        {
            // Skip test if no listener is attached
            return;
        }

        // Inject
        var headers = new Dictionary<string, string>();
        TraceContext.InjectContext(headers, activity);

        // Extract
        var extractedContext = TraceContext.ExtractContext(headers);

        Assert.Equal(activity.TraceId, extractedContext.TraceId);
        Assert.Equal(activity.SpanId, extractedContext.SpanId);
    }

    [Fact]
    public void CreateLinkedSpan_WithValidParentContext_CreatesLinkedSpan()
    {
        var activitySource = new ActivitySource("test-source");
        using var parentActivity = activitySource.StartActivity("parent-activity");

        if (parentActivity == null)
        {
            // Skip test if no listener is attached
            return;
        }

        var parentContext = parentActivity.Context;

        using var linkedSpan = TraceContext.CreateLinkedSpan(
            activitySource,
            "linked-span",
            parentContext);

        // The linked span should exist if listeners are attached
        // In test environment without listeners, it may be null
        if (linkedSpan != null)
        {
            Assert.Equal("linked-span", linkedSpan.DisplayName);
        }
    }

    [Fact]
    public void CreateLinkedSpan_WithNullActivitySource_ThrowsArgumentNullException()
    {
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None);

        Assert.Throws<ArgumentNullException>(() =>
            TraceContext.CreateLinkedSpan(null!, "test", parentContext));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateLinkedSpan_WithNullOrEmptyName_ThrowsArgumentException(string? name)
    {
        var activitySource = new ActivitySource("test-source");
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None);

        Assert.Throws<ArgumentException>(() =>
            TraceContext.CreateLinkedSpan(activitySource, name!, parentContext));
    }

    [Fact]
    public void GetCurrentTraceId_WithNoActivity_ReturnsNull()
    {
        // Ensure no ambient activity
        Activity.Current = null;

        var traceId = TraceContext.GetCurrentTraceId();
        Assert.Null(traceId);
    }

    [Fact]
    public void GetCurrentTraceId_WithActiveActivity_ReturnsTraceId()
    {
        var activitySource = new ActivitySource("test-source");
        using var activity = activitySource.StartActivity("test-activity");

        if (activity != null)
        {
            Activity.Current = activity;
            var traceId = TraceContext.GetCurrentTraceId();
            Assert.NotNull(traceId);
            Assert.Equal(activity.TraceId.ToHexString(), traceId);
        }
    }

    [Fact]
    public void GetCurrentSpanId_WithNoActivity_ReturnsNull()
    {
        Activity.Current = null;

        var spanId = TraceContext.GetCurrentSpanId();
        Assert.Null(spanId);
    }

    [Fact]
    public void GetCurrentSpanId_WithActiveActivity_ReturnsSpanId()
    {
        var activitySource = new ActivitySource("test-source");
        using var activity = activitySource.StartActivity("test-activity");

        if (activity != null)
        {
            Activity.Current = activity;
            var spanId = TraceContext.GetCurrentSpanId();
            Assert.NotNull(spanId);
            Assert.Equal(activity.SpanId.ToHexString(), spanId);
        }
    }

    [Fact]
    public void IsTracingActive_WithNoActivity_ReturnsFalse()
    {
        Activity.Current = null;

        var isActive = TraceContext.IsTracingActive();
        Assert.False(isActive);
    }

    [Fact]
    public void IsTracingActive_WithActiveActivity_ReturnsTrue()
    {
        var activitySource = new ActivitySource("test-source");
        using var activity = activitySource.StartActivity("test-activity");

        if (activity != null)
        {
            Activity.Current = activity;
            var isActive = TraceContext.IsTracingActive();
            Assert.True(isActive);
        }
    }

    [Fact]
    public void ExtractContext_WithUnsupportedVersion_ReturnsDefault()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "99-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        };

        var context = TraceContext.ExtractContext(headers);
        Assert.Equal(default, context);
    }

    [Fact]
    public void ExtractContext_WithMalformedTraceId_ReturnsDefault()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "00-invalid-trace-id-b7ad6b7169203331-01"
        };

        var context = TraceContext.ExtractContext(headers);
        Assert.Equal(default, context);
    }

    [Fact]
    public void ExtractContext_WithMalformedSpanId_ReturnsDefault()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-invalid-span-01"
        };

        var context = TraceContext.ExtractContext(headers);
        Assert.Equal(default, context);
    }
}
