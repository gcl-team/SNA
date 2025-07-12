using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Modeling.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimNextgenApp.Tests.Modeling.Resource;

public class ResourcePoolTests
{
    private const string DefaultPoolName = "TestPool";
    private readonly Mock<IRunContext> _mockEngineContext;
    private readonly ILoggerFactory _nullLoggerFactory;
    private double _currentTestTime = 0.0;

    public ResourcePoolTests()
    {
        _nullLoggerFactory = NullLoggerFactory.Instance;

        _mockEngineContext = new Mock<IRunContext>();
        _mockEngineContext.SetupGet(e => e.ClockTime).Returns(() => _currentTestTime);
    }

    // Helper method to create a default list of resources
    private List<DummyResource> CreateDefaultResources(int count = 3)
    {
        return Enumerable.Range(1, count).Select(i => new DummyResource(i)).ToList();
    }

    // Factory method to create a ResourcePool instance for tests
    private ResourcePool<DummyResource> CreatePool(
        IEnumerable<DummyResource>? resources = null,
        string name = DefaultPoolName)
    {
        return new ResourcePool<DummyResource>(
            resources,
            name,
            _nullLoggerFactory
        );
    }

    [Fact]
    public void Constructor_WithValidResources_InitializesCorrectly()
    {
        // Arrange
        var resources = CreateDefaultResources(5);
        var pool = CreatePool(resources: resources, name: "ValidPool");

        // Assert
        Assert.Equal("ValidPool", pool.Name);
        Assert.Equal(5, pool.TotalCapacity);
        Assert.Equal(5, pool.AvailableCount);
        Assert.Equal(0, pool.BusyCount);
        Assert.NotNull(pool.UtilizationMetric);
    }

    [Fact]
    public void Constructor_WithNullResources_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(
            "resources",
            () => CreatePool(resources: null)
        );
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(
            "loggerFactory",
            () => new ResourcePool<DummyResource>(CreateDefaultResources(), DefaultPoolName, null!)
        );
    }

    [Fact]
    public void TryAcquire_WhenResourcesAvailable_SucceedsAndUpdatesState()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(3));
        _currentTestTime = 10.0;

        // Act
        var acquiredResource = pool.TryAcquire(_mockEngineContext.Object);

        // Assert
        Assert.NotNull(acquiredResource);
        Assert.Equal(3, pool.TotalCapacity);
        Assert.Equal(2, pool.AvailableCount);
        Assert.Equal(1, pool.BusyCount);
        Assert.Equal(1, pool.UtilizationMetric.CurrentCount); // BusyCount should be observed
    }

    [Fact]
    public void TryAcquire_WhenPoolBecomesEmpty_ReturnsNullOnNextAttempt()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(1));

        // Act
        var firstAcquire = pool.TryAcquire(_mockEngineContext.Object);
        var secondAcquire = pool.TryAcquire(_mockEngineContext.Object);

        // Assert
        Assert.NotNull(firstAcquire);
        Assert.Null(secondAcquire);
        Assert.Equal(1, pool.TotalCapacity);
        Assert.Equal(0, pool.AvailableCount);
        Assert.Equal(1, pool.BusyCount);
        Assert.Equal(1, pool.UtilizationMetric.CurrentCount);
    }

    [Fact]
    public void Release_WithValidResource_SucceedsAndUpdatesState()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(3));
        var resourceToRelease = pool.TryAcquire(_mockEngineContext.Object);
        Assert.NotNull(resourceToRelease); // Pre-condition check

        _currentTestTime = 20.0;

        // Act
        pool.Release(resourceToRelease, _mockEngineContext.Object);

        // Assert
        Assert.Equal(3, pool.TotalCapacity);
        Assert.Equal(3, pool.AvailableCount);
        Assert.Equal(0, pool.BusyCount);
        Assert.Equal(0, pool.UtilizationMetric.CurrentCount);
    }

    [Fact]
    public void Release_WithNullResource_ThrowsArgumentNullException()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(3));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            "resource",
            () => pool.Release(null!, _mockEngineContext.Object)
        );
    }

    [Fact]
    public void Release_WhenResourceIsAlreadyIdle_DoesNotChangeState()
    {
        // Arrange
        var resources = CreateDefaultResources(2);
        var pool = CreatePool(resources: resources);
        var resourceToDoubleRelease = resources.First();

        // Act
        pool.Release(resourceToDoubleRelease, _mockEngineContext.Object); // Should be logged as an error and ignored

        // Assert
        Assert.Equal(2, pool.TotalCapacity);
        Assert.Equal(2, pool.AvailableCount); // Count should not increase beyond capacity
        Assert.Equal(0, pool.BusyCount);
    }

    [Fact]
    public void Events_ResourceAcquired_IsFiredOnSuccessfulAcquire()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(3));
        DummyResource? acquiredResource = null;
        double eventTime = -1;

        pool.ResourceAcquired += (res, time) => {
            acquiredResource = res;
            eventTime = time;
        };
        _currentTestTime = 5.0;

        // Act
        var result = pool.TryAcquire(_mockEngineContext.Object);

        // Assert
        Assert.NotNull(acquiredResource);
        Assert.Equal(result, acquiredResource);
        Assert.Equal(5.0, eventTime);
    }

    [Fact]
    public void Events_RequestFailed_IsFiredOnFailedAcquire()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(0)); // An empty pool
        bool wasFired = false;
        double eventTime = -1;

        pool.RequestFailed += (time) => {
            wasFired = true;
            eventTime = time;
        };
        _currentTestTime = 7.0;

        // Act
        pool.TryAcquire(_mockEngineContext.Object);

        // Assert
        Assert.True(wasFired);
        Assert.Equal(7.0, eventTime);
    }

    [Fact]
    public void Events_ResourceReleased_IsFiredOnSuccessfulRelease()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(3));
        var resourceToRelease = pool.TryAcquire(_mockEngineContext.Object)!;

        DummyResource? releasedResource = null;
        double eventTime = -1;

        pool.ResourceReleased += (res, time) => {
            releasedResource = res;
            eventTime = time;
        };
        _currentTestTime = 15.0;

        // Act
        pool.Release(resourceToRelease, _mockEngineContext.Object);

        // Assert
        Assert.NotNull(releasedResource);
        Assert.Equal(resourceToRelease, releasedResource);
        Assert.Equal(15.0, eventTime);
    }

    [Fact]
    public void Initialize_SetsUtilizationMetricToBaseState()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(3));
        // Mess up the metric to ensure Initialize fixes it
        pool.TryAcquire(_mockEngineContext.Object);
        Assert.NotEqual(0, pool.UtilizationMetric.CurrentCount);

        // Act
        pool.Initialize(_mockEngineContext.Object);

        // Assert
        Assert.Equal(0, pool.UtilizationMetric.CurrentCount);
    }

    [Fact]
    public void WarmedUp_ResetsMetricAndObservesCurrentState()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(5));
        pool.TryAcquire(_mockEngineContext.Object);
        pool.TryAcquire(_mockEngineContext.Object);
        Assert.Equal(2, pool.BusyCount);

        double warmupTime = 100.0;

        // Act
        pool.WarmedUp(warmupTime);

        // Assert
        // Metric should be reset and then the current BusyCount (2) observed.
        Assert.Equal(2, pool.UtilizationMetric.CurrentCount);
    }
}
