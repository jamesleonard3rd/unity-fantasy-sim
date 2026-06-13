namespace FantasySim.Core.Ids
{
    public readonly struct EntityId
    {
        public EntityId(long value)
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
