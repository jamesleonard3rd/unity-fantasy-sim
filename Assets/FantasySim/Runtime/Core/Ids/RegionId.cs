namespace FantasySim.Core.Ids
{
    public readonly struct RegionId
    {
        public RegionId(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
