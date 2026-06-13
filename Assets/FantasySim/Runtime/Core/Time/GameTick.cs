namespace FantasySim.Core.Time
{
    public readonly record struct GameTick(long Value)
    {
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
