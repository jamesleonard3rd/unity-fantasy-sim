namespace FantasySim.Persistence.SQLite
{
    public sealed class StarterWorldGenerationResult
    {
        public StarterWorldGenerationResult(
            bool generated,
            string message,
            int regionsInserted,
            int partitionsInserted,
            int entitiesInserted
        )
        {
            Generated = generated;
            Message = message;
            RegionsInserted = regionsInserted;
            PartitionsInserted = partitionsInserted;
            EntitiesInserted = entitiesInserted;
        }

        public bool Generated { get; }
        public string Message { get; }
        public int RegionsInserted { get; }
        public int PartitionsInserted { get; }
        public int EntitiesInserted { get; }
    }
}
