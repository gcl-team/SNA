namespace SimNextgenApp.Demo.CustomModels;

internal class MyLoad
{
    private static int _nextId = 0;
    public int Id { get; }
    public long CreationTime { get; set; }
    public long ServiceStartTime { get; set; }
    public long ServiceEndTime { get; set; }

    public MyLoad()
    {
        Id = Interlocked.Increment(ref _nextId);
    }

    public override string ToString()
    {
        return $"MyLoad (ID: {Id})";
    }
}