using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Modeling.Resource;
using System.ComponentModel;

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

    [Fact(DisplayName = "Constructor with valid resources should initialise properties correctly.")]
    public void Constructor_WithValidResources_InitializesCorrectly()
    {
        // Arrange
        var resources = CreateDefaultResources(5);
        var pool = CreatePool(resources, "ValidPool");

        // Assert
        Assert.Equal("ValidPool", pool.Name);
        Assert.Equal(5, pool.TotalCapacity);
        Assert.Equal(5, pool.AvailableCount);
        Assert.Equal(0, pool.BusyCount);
        Assert.NotNull(pool.UtilizationMetric);
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException for null resource collection.")]
    public void Constructor_WithNullResources_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(
            "resources",
            () => CreatePool(resources: null)
        );
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException for null logger factory.")]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(
            "loggerFactory",
            () => new ResourcePool<DummyResource>(CreateDefaultResources(), DefaultPoolName, null!)
        );
    }

    [Fact(DisplayName = "Initialize should reset the utilisation metric to its base state.")]
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

    [Fact(DisplayName = "TryAcquire should succeed and update state when resources are available.")]
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

    [Fact(DisplayName = "TryAcquire should return null when no resources are available.")]
    public void TryAcquire_WhenPoolIsEmpty_ReturnsNull()
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

    [Fact(DisplayName = "Release should succeed and update state for a valid resource.")]
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

    [Fact(DisplayName = "Release should throw ArgumentNullException for a null resource.")]
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

    [Fact(DisplayName = "Release should not change state if resource is already idle (double-release).")]
    public void Release_WhenResourceIsAlreadyIdle_StateIsUnchanged()
    {
        // Arrange
        var resources = CreateDefaultResources(2);
        var pool = CreatePool(resources: resources);
        var resourceToDoubleRelease = resources.First();

        // Act
        pool.Release(resourceToDoubleRelease, _mockEngineContext.Object);

        // Assert
        Assert.Equal(2, pool.TotalCapacity);
        Assert.Equal(2, pool.AvailableCount); // Count should not increase beyond capacity
        Assert.Equal(0, pool.BusyCount);
    }

    [Fact(DisplayName = "ResourceAcquired event should fire on a successful acquire.")]
    public void Events_ResourceAcquired_IsFiredOnSuccess()
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

    [Fact(DisplayName = "RequestFailed event should fire on a failed acquire.")]
    public void Events_RequestFailed_IsFiredOnFailedAcquire()
    {
        // Arrange
        var pool = CreatePool(resources: CreateDefaultResources(0)); // An empty pool
        bool isFired = false;
        double eventTime = -1;

        pool.RequestFailed += (time) => {
            isFired = true;
            eventTime = time;
        };
        _currentTestTime = 7.0;

        // Act
        pool.TryAcquire(_mockEngineContext.Object);

        // Assert
        Assert.True(isFired);
        Assert.Equal(7.0, eventTime);
    }

    [Fact(DisplayName = "ResourceReleased event should fire on a successful release.")]
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

    [Fact(DisplayName = "WarmedUp should reset the metric and observe the current busy count.")]
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
