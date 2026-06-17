namespace FantasySim.Persistence.SQLite
{
    public sealed class StarterWorldGenerationResult
    {
        public StarterWorldGenerationResult(
            bool generated,
            string message,
            int regionsInserted,
            int partitionsInserted,
            int entitiesInserted,
            int relationshipsInserted = 0,
            int relationshipTermsInserted = 0,
            int entityGoalsInserted = 0
        )
        {
            Generated = generated;
            Message = message;
            RegionsInserted = regionsInserted;
            PartitionsInserted = partitionsInserted;
            EntitiesInserted = entitiesInserted;
            RelationshipsInserted = relationshipsInserted;
            RelationshipTermsInserted = relationshipTermsInserted;
            EntityGoalsInserted = entityGoalsInserted;
        }

        public bool Generated { get; }
        public string Message { get; }
        public int RegionsInserted { get; }
        public int PartitionsInserted { get; }
        public int EntitiesInserted { get; }
        public int RelationshipsInserted { get; }
        public int RelationshipTermsInserted { get; }
        public int EntityGoalsInserted { get; }
    }
}
