namespace FantasySim.Core.Random
{
    public interface IDeterministicRandom
    {
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
    }
}
