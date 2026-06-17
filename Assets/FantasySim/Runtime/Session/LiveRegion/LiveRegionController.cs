using System.Collections.Generic;
using FantasySim.Core.Ids;
using FantasySim.Persistence.SQLite;
using FantasySim.Simulation.State;
using UnityEngine;

namespace FantasySim.Session
{
    // Minimal live-region sandbox host. On enable it marks a region live (so the coarse
    // scheduler skips it), spawns a placeholder capsule per entity from saved region state,
    // and logs the "while you were away" summary. On disable it writes capsule positions
    // back to SQLite and restores coarse authority.
    public sealed class LiveRegionController : MonoBehaviour
    {
        [Tooltip("Simulation partition key to enter. Defaults to the starter world.")]
        [SerializeField]
        private string partitionKey = LiveRegionAuthorityService.DefaultPartitionKey;

        [Tooltip("Region key to make live. Leave empty to use the first region in the partition.")]
        [SerializeField]
        private string regionKey = string.Empty;

        [Tooltip("Authority owner recorded on the live region.")]
        [SerializeField]
        private string authorityOwner = LiveRegionAuthorityService.DefaultOwner;

        [Tooltip("Log the region-entry summary to the console when the region goes live.")]
        [SerializeField]
        private bool logSummary = true;

        private LiveRegionAuthorityService authorityService;
        private readonly List<SpawnedActor> spawnedActors = new List<SpawnedActor>();
        private RegionId liveRegionId;
        private FantasySim.Core.Time.GameTick seenThroughTick;
        private bool isLive;

        private void OnEnable()
        {
            authorityService = new LiveRegionAuthorityService();
            LiveRegionEntry entry = authorityService.EnterLiveRegion(
                string.IsNullOrWhiteSpace(partitionKey) ? LiveRegionAuthorityService.DefaultPartitionKey : partitionKey,
                string.IsNullOrWhiteSpace(regionKey) ? null : regionKey,
                string.IsNullOrWhiteSpace(authorityOwner) ? LiveRegionAuthorityService.DefaultOwner : authorityOwner
            );

            liveRegionId = entry.Region.Id;
            seenThroughTick = entry.CurrentTick;
            isLive = true;

            SpawnActors(entry.Entities);

            if (logSummary)
            {
                Debug.Log(
                    $"Entered live region '{entry.Region.DisplayName}' (id {entry.Region.Id.Value}) " +
                    $"with {entry.Entities.Count} NPC(s).\n{entry.Summary.ToDisplayText()}"
                );
            }
        }

        private void OnDisable()
        {
            if (isLive && authorityService != null)
            {
                authorityService.ExitLiveRegion(liveRegionId, CapturePositions(), seenThroughTick);
            }

            DestroyActors();
            authorityService = null;
            isLive = false;
        }

        private void SpawnActors(IReadOnlyList<EntityState> entities)
        {
            for (int index = 0; index < entities.Count; index++)
            {
                EntityState entity = entities[index];
                GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                actor.name = $"NPC_{entity.Id.Value}_{entity.DisplayName}";
                actor.transform.SetParent(transform, false);
                actor.transform.localPosition = ResolveSpawnPosition(entity, index);
                actor.transform.localRotation = Quaternion.Euler(0f, entity.HeadingY ?? 0f, 0f);

                spawnedActors.Add(new SpawnedActor(entity.Id, entity.RegionId, actor.transform));
            }
        }

        private static Vector3 ResolveSpawnPosition(EntityState entity, int index)
        {
            if (entity.LocalX.HasValue || entity.LocalY.HasValue || entity.LocalZ.HasValue)
            {
                return new Vector3(entity.LocalX ?? 0f, entity.LocalY ?? 0f, entity.LocalZ ?? 0f);
            }

            // Lay out positionless entities in a simple deterministic grid so they are visible.
            const int columns = 8;
            const float spacing = 2f;
            int row = index / columns;
            int column = index % columns;
            return new Vector3(column * spacing, 0f, row * spacing);
        }

        private List<EntityPositionUpdate> CapturePositions()
        {
            var positions = new List<EntityPositionUpdate>(spawnedActors.Count);
            foreach (SpawnedActor actor in spawnedActors)
            {
                if (actor.Transform == null)
                {
                    continue;
                }

                Vector3 localPosition = actor.Transform.localPosition;
                float headingY = actor.Transform.localRotation.eulerAngles.y;
                positions.Add(new EntityPositionUpdate(
                    actor.EntityId,
                    actor.RegionId,
                    localPosition.x,
                    localPosition.y,
                    localPosition.z,
                    headingY
                ));
            }

            return positions;
        }

        private void DestroyActors()
        {
            foreach (SpawnedActor actor in spawnedActors)
            {
                if (actor.Transform != null)
                {
                    Destroy(actor.Transform.gameObject);
                }
            }

            spawnedActors.Clear();
        }

        private readonly struct SpawnedActor
        {
            public SpawnedActor(EntityId entityId, RegionId regionId, Transform actorTransform)
            {
                EntityId = entityId;
                RegionId = regionId;
                Transform = actorTransform;
            }

            public EntityId EntityId { get; }
            public RegionId RegionId { get; }
            public Transform Transform { get; }
        }
    }
}
