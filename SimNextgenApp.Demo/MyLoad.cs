namespace SimNextgenApp.Demo;

internal class MyLoad
{
    private static int _nextId = 0;
    public int Id { get; }
    public double CreationTime { get; set; } // Simulation time when created

    public MyLoad()
    {
        Id = Interlocked.Increment(ref _nextId);
    }

    public override string ToString()
    {
        return $"MyLoad (ID: {Id}, CreatedAt: {CreationTime:F2})";
    }
}
