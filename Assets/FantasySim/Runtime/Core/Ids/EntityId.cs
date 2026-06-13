namespace FantasySim.Core.Ids
{
    public readonly record struct EntityId(long Value)
    {
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
