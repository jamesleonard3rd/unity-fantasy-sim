using System;
using System.IO;
using System.Linq;
using FantasySim.Content.Definitions;
using FantasySim.Content.Loaders;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Content
{
    public sealed class JsonContentLoaderTests
    {
        private string tempDirectory;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimContentTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void LoadersNormalizeRepresentativeRealGameData()
        {
            string root = GameDataJsonPaths.DefaultRoot;

            FactionDefinition whiteTower = new FactionJsonLoader(GameDataJsonPaths.Factions(root))
                .Load()
                .Single(definition => definition.key == "the_white_tower");
            RaceDefinition human = new RaceJsonLoader(GameDataJsonPaths.Races(root))
                .Load()
                .Single(definition => definition.key == "human");
            StatDefinition strength = new StatJsonLoader(GameDataJsonPaths.Stats(root))
                .Load()
                .Single(definition => definition.key == "strength");
            TraitDefinition strong = new TraitJsonLoader(GameDataJsonPaths.Traits(root))
                .Load()
                .Single(definition => definition.key == "strong");
            RegionDefinition whisperwood = new RegionTemplateJsonLoader(GameDataJsonPaths.Regions(root))
                .Load()
                .Single(definition => definition.key == "whisperwood_marches");
            EntityTemplateDefinition adrien = new EntityTemplateJsonLoader(GameDataJsonPaths.EntityTemplates(root))
                .Load()
                .Single(definition => definition.key == "adrien_valemont");

            Assert.That(whiteTower.displayName, Is.EqualTo("The White Tower"));
            Assert.That(human.subraces[0].key, Is.EqualTo("varenic"));
            Assert.That(strength.displayName, Is.EqualTo("Strength"));
            Assert.That(strong.modifiers[0].statKey, Is.EqualTo("strength"));
            Assert.That(whisperwood.tickIntervalSeconds, Is.EqualTo(180f));
            Assert.That(adrien.raceKey, Is.EqualTo("human"));
            Assert.That(adrien.houseKey, Is.EqualTo("house_valemont"));
            Assert.That(adrien.regionKey, Is.EqualTo("whisperwood_marches"));
            Assert.That(adrien.traitKeys, Contains.Item("natural_leader"));
        }

        [Test]
        public void TravelEdgeLoaderNormalizesRealGameData()
        {
            string root = GameDataJsonPaths.DefaultRoot;

            TravelEdgeDefinition gate = new TravelEdgeJsonLoader(GameDataJsonPaths.Regions(root))
                .Load()
                .Single(edge => edge.fromKey == "valemont_keep" && edge.toKey == "whisperwood_marches");

            Assert.That(gate.mode, Is.EqualTo("gate"));
            Assert.That(gate.baseTicks, Is.EqualTo(10));
            Assert.That(gate.bidirectional, Is.True);
        }

        [Test]
        public void TravelEdgeLoaderNormalizesEndpointKeysAndReportsNegativeCost()
        {
            string path = WriteJson("regions.json", @"{
  ""regions"": [],
  ""travel_edges"": [
    { ""from"": ""Valemont Keep"", ""to"": ""whisperwood_marches"", ""mode"": ""gate"", ""base_ticks"": -1, ""bidirectional"": true }
  ]
}");
            var loader = new TravelEdgeJsonLoader(path);

            var edges = loader.Load();
            var messages = loader.Validate(edges);

            Assert.That(edges.Single().fromKey, Is.EqualTo("valemont_keep"));
            Assert.That(edges.Single().toKey, Is.EqualTo("whisperwood_marches"));
            Assert.That(messages.Any(message => message.Code == "content.invalid_definition"), Is.True);
        }

        [Test]
        public void RegionLoaderUsesDefaultTickIntervalWhenJsonOmitsIt()
        {
            string path = WriteJson("regions.json", @"{
  ""regions"": [
    { ""key"": ""test_keep"", ""name"": ""Test Keep"", ""type"": ""castle"" }
  ]
}");

            RegionDefinition region = new RegionTemplateJsonLoader(path).Load().Single();

            Assert.That(region.tickIntervalSeconds, Is.EqualTo(RegionTemplateJsonLoader.DefaultTickIntervalSeconds));
        }

        [Test]
        public void LoaderValidationReportsAmbiguousNormalizedDisplayNames()
        {
            string path = WriteJson("factions.json", @"{
  ""factions"": [
    { ""name"": ""White Tower"", ""type"": ""guild"" },
    { ""name"": ""White-Tower"", ""type"": ""guild"" }
  ]
}");
            var loader = new FactionJsonLoader(path);

            var messages = loader.Validate(loader.Load());

            Assert.That(messages.Any(message => message.Code == "content.ambiguous_display_name"), Is.True);
        }

        [Test]
        public void LoaderValidationReportsMissingDisplayNames()
        {
            string path = WriteJson("stats.json", @"{
  ""stats"": [
    { ""description"": ""Missing name."" }
  ]
}");
            var loader = new StatJsonLoader(path);

            var messages = loader.Validate(loader.Load());

            Assert.That(messages.Any(message => message.Code == "content.missing_key"), Is.True);
            Assert.That(messages.Any(message => message.Code == "content.missing_display_name"), Is.True);
        }

        private string WriteJson(string fileName, string json)
        {
            string path = Path.Combine(tempDirectory, fileName);
            File.WriteAllText(path, json);
            return path;
        }
    }
}
