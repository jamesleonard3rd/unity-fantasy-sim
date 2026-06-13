namespace FantasySim.Core.Ids
{
    public readonly record struct RegionId(long Value)
    {
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
