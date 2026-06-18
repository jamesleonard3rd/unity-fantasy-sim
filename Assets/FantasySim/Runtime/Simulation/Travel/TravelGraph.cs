using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;

namespace FantasySim.Simulation.Travel
{
    /// <summary>
    /// Cached region connectivity built from authored travel edges plus derived local
    /// edges (regions reachable from their parent/siblings). Works purely in stable region
    /// keys; route costs are measured in coarse simulation ticks.
    /// </summary>
    public sealed class TravelGraph
    {
        public const int DefaultLocalEdgeTicks = 0;

        private readonly Dictionary<string, Dictionary<string, int>> adjacency;

        private TravelGraph(Dictionary<string, Dictionary<string, int>> adjacency)
        {
            this.adjacency = adjacency;
        }

        public static TravelGraph Build(ContentCatalog catalog, int localEdgeTicks = DefaultLocalEdgeTicks)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            IReadOnlyCollection<RegionDefinition> regions = catalog.Regions != null
                ? catalog.Regions.Values.ToList()
                : (IReadOnlyCollection<RegionDefinition>)Array.Empty<RegionDefinition>();

            return Build(catalog.TravelEdges, regions, localEdgeTicks);
        }

        public static TravelGraph Build(
            IReadOnlyList<TravelEdgeDefinition> travelEdges,
            IReadOnlyCollection<RegionDefinition> regions,
            int localEdgeTicks = DefaultLocalEdgeTicks
        )
        {
            var adjacency = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

            if (travelEdges != null)
            {
                foreach (TravelEdgeDefinition edge in travelEdges)
                {
                    if (edge == null || string.IsNullOrWhiteSpace(edge.fromKey) || string.IsNullOrWhiteSpace(edge.toKey))
                    {
                        continue;
                    }

                    int cost = Math.Max(0, edge.baseTicks);
                    AddEdge(adjacency, edge.fromKey, edge.toKey, cost);
                    if (edge.bidirectional)
                    {
                        AddEdge(adjacency, edge.toKey, edge.fromKey, cost);
                    }
                }
            }

            AddDerivedLocalEdges(adjacency, regions, Math.Max(0, localEdgeTicks));

            return new TravelGraph(adjacency);
        }

        /// <summary>
        /// Cost in ticks to travel directly between two adjacent regions, or -1 when they are
        /// not directly connected.
        /// </summary>
        public int GetEdgeCost(string fromKey, string toKey)
        {
            return TryGetEdgeCost(fromKey, toKey, out int cost) ? cost : -1;
        }

        public bool TryGetEdgeCost(string fromKey, string toKey, out int cost)
        {
            cost = -1;
            if (string.IsNullOrWhiteSpace(fromKey) || string.IsNullOrWhiteSpace(toKey))
            {
                return false;
            }

            if (adjacency.TryGetValue(fromKey, out Dictionary<string, int> neighbors)
                && neighbors.TryGetValue(toKey, out int edgeCost))
            {
                cost = edgeCost;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cheapest route (by tick cost) as an ordered list of region keys after the origin.
        /// Returns an empty list when already at the destination, and null when unreachable.
        /// </summary>
        public IReadOnlyList<string> FindRoute(string fromKey, string toKey)
        {
            if (string.IsNullOrWhiteSpace(fromKey) || string.IsNullOrWhiteSpace(toKey))
            {
                return null;
            }

            if (string.Equals(fromKey, toKey, StringComparison.Ordinal))
            {
                return Array.Empty<string>();
            }

            var distances = new Dictionary<string, int>(StringComparer.Ordinal) { [fromKey] = 0 };
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (true)
            {
                string current = SelectClosestUnvisited(distances, visited);
                if (current == null)
                {
                    break;
                }

                if (string.Equals(current, toKey, StringComparison.Ordinal))
                {
                    return ReconstructPath(previous, fromKey, toKey);
                }

                visited.Add(current);
                int currentDistance = distances[current];

                if (!adjacency.TryGetValue(current, out Dictionary<string, int> neighbors))
                {
                    continue;
                }

                foreach (KeyValuePair<string, int> neighbor in neighbors)
                {
                    if (visited.Contains(neighbor.Key))
                    {
                        continue;
                    }

                    int candidate = currentDistance + neighbor.Value;
                    if (!distances.TryGetValue(neighbor.Key, out int existing) || candidate < existing)
                    {
                        distances[neighbor.Key] = candidate;
                        previous[neighbor.Key] = current;
                    }
                }
            }

            return null;
        }

        private static void AddEdge(
            Dictionary<string, Dictionary<string, int>> adjacency,
            string fromKey,
            string toKey,
            int cost
        )
        {
            if (string.Equals(fromKey, toKey, StringComparison.Ordinal))
            {
                return;
            }

            if (!adjacency.TryGetValue(fromKey, out Dictionary<string, int> neighbors))
            {
                neighbors = new Dictionary<string, int>(StringComparer.Ordinal);
                adjacency.Add(fromKey, neighbors);
            }

            if (!neighbors.TryGetValue(toKey, out int existing) || cost < existing)
            {
                neighbors[toKey] = cost;
            }
        }

        private static void AddDerivedLocalEdges(
            Dictionary<string, Dictionary<string, int>> adjacency,
            IReadOnlyCollection<RegionDefinition> regions,
            int localEdgeTicks
        )
        {
            if (regions == null)
            {
                return;
            }

            var childrenByParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (RegionDefinition region in regions)
            {
                if (region == null || string.IsNullOrWhiteSpace(region.key) || string.IsNullOrWhiteSpace(region.parentKey))
                {
                    continue;
                }

                if (!childrenByParent.TryGetValue(region.parentKey, out List<string> children))
                {
                    children = new List<string>();
                    childrenByParent.Add(region.parentKey, children);
                }

                children.Add(region.key);
            }

            foreach (KeyValuePair<string, List<string>> entry in childrenByParent)
            {
                string parentKey = entry.Key;
                List<string> children = entry.Value;

                foreach (string childKey in children)
                {
                    AddEdge(adjacency, parentKey, childKey, localEdgeTicks);
                    AddEdge(adjacency, childKey, parentKey, localEdgeTicks);
                }

                for (int i = 0; i < children.Count; i++)
                {
                    for (int j = i + 1; j < children.Count; j++)
                    {
                        AddEdge(adjacency, children[i], children[j], localEdgeTicks);
                        AddEdge(adjacency, children[j], children[i], localEdgeTicks);
                    }
                }
            }
        }

        private static string SelectClosestUnvisited(IReadOnlyDictionary<string, int> distances, HashSet<string> visited)
        {
            string closest = null;
            int bestDistance = int.MaxValue;
            foreach (KeyValuePair<string, int> entry in distances)
            {
                if (visited.Contains(entry.Key))
                {
                    continue;
                }

                if (entry.Value < bestDistance
                    || (entry.Value == bestDistance && (closest == null || string.CompareOrdinal(entry.Key, closest) < 0)))
                {
                    bestDistance = entry.Value;
                    closest = entry.Key;
                }
            }

            return closest;
        }

        private static IReadOnlyList<string> ReconstructPath(
            IReadOnlyDictionary<string, string> previous,
            string fromKey,
            string toKey
        )
        {
            var reversed = new List<string>();
            string current = toKey;
            while (!string.Equals(current, fromKey, StringComparison.Ordinal))
            {
                reversed.Add(current);
                if (!previous.TryGetValue(current, out current))
                {
                    return null;
                }
            }

            reversed.Reverse();
            return reversed;
        }
    }
}
