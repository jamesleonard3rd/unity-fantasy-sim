namespace FantasySim.Session
{
    public interface ITimeSource
    {
        double NowSeconds { get; }
    }
}
