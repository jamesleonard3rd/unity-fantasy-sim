using System.Collections.Generic;
using System.Linq;
using FantasySim.Content.Definitions;
using FantasySim.Simulation.Travel;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class TravelGraphTests
    {
        [Test]
        public void FindRouteReturnsEmptyWhenAlreadyAtDestination()
        {
            TravelGraph graph = TravelGraph.Build(
                new[] { Edge("a", "b", 5, bidirectional: true) },
                NoRegions()
            );

            IReadOnlyList<string> route = graph.FindRoute("a", "a");

            Assert.That(route, Is.Empty);
        }

        [Test]
        public void FindRouteFollowsBidirectionalEdgeBothWays()
        {
            TravelGraph graph = TravelGraph.Build(
                new[] { Edge("a", "b", 5, bidirectional: true) },
                NoRegions()
            );

            Assert.That(graph.FindRoute("a", "b"), Is.EqualTo(new[] { "b" }));
            Assert.That(graph.FindRoute("b", "a"), Is.EqualTo(new[] { "a" }));
            Assert.That(graph.GetEdgeCost("a", "b"), Is.EqualTo(5));
            Assert.That(graph.GetEdgeCost("b", "a"), Is.EqualTo(5));
        }

        [Test]
        public void FindRouteRespectsEdgeDirectionForOneWayEdges()
        {
            TravelGraph graph = TravelGraph.Build(
                new[] { Edge("a", "b", 3, bidirectional: false) },
                NoRegions()
            );

            Assert.That(graph.FindRoute("a", "b"), Is.EqualTo(new[] { "b" }));
            Assert.That(graph.FindRoute("b", "a"), Is.Null);
        }

        [Test]
        public void FindRoutePicksCheapestPathByTickCost()
        {
            TravelGraph graph = TravelGraph.Build(
                new[]
                {
                    Edge("a", "b", 10, bidirectional: true),
                    Edge("b", "c", 10, bidirectional: true),
                    Edge("a", "c", 30, bidirectional: true)
                },
                NoRegions()
            );

            IReadOnlyList<string> route = graph.FindRoute("a", "c");

            Assert.That(route, Is.EqualTo(new[] { "b", "c" }));
        }

        [Test]
        public void FindRouteReturnsNullWhenUnreachable()
        {
            TravelGraph graph = TravelGraph.Build(
                new[] { Edge("a", "b", 5, bidirectional: true) },
                NoRegions()
            );

            Assert.That(graph.FindRoute("a", "isolated"), Is.Null);
        }

        [Test]
        public void DerivedLocalEdgesConnectParentAndSiblingRegions()
        {
            var regions = new List<RegionDefinition>
            {
                new RegionDefinition { key = "keep", type = "castle" },
                new RegionDefinition { key = "market", type = "market", parentKey = "keep" },
                new RegionDefinition { key = "barracks", type = "barracks", parentKey = "keep" }
            };
            TravelGraph graph = TravelGraph.Build(System.Array.Empty<TravelEdgeDefinition>(), regions);

            Assert.That(graph.FindRoute("keep", "market"), Is.EqualTo(new[] { "market" }));
            Assert.That(graph.FindRoute("market", "barracks"), Is.EqualTo(new[] { "barracks" }));
            Assert.That(graph.GetEdgeCost("keep", "market"), Is.EqualTo(TravelGraph.DefaultLocalEdgeTicks));
        }

        private static TravelEdgeDefinition Edge(string fromKey, string toKey, int baseTicks, bool bidirectional)
        {
            return new TravelEdgeDefinition
            {
                fromKey = fromKey,
                toKey = toKey,
                mode = "road",
                baseTicks = baseTicks,
                bidirectional = bidirectional
            };
        }

        private static IReadOnlyCollection<RegionDefinition> NoRegions()
        {
            return System.Array.Empty<RegionDefinition>();
        }
    }
}
