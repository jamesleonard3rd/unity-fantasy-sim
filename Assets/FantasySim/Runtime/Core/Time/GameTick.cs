namespace FantasySim.Core.Time
{
    public readonly struct GameTick
    {
        public GameTick(long value)
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
