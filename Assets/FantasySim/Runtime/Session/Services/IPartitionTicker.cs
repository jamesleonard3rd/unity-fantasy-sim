namespace FantasySim.Session
{
    public interface IPartitionTicker
    {
        PartitionTickReport TickPartition(long partitionId);
    }
}
