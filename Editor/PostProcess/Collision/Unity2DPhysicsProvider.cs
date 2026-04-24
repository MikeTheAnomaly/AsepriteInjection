using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class Unity2DPhysicsProvider
    {
        public static void Apply(List<Tilemap> tilemaps)
        {
            if (tilemaps == null)
            {
                return;
            }

            foreach (var tilemap in tilemaps)
            {
                if (tilemap == null)
                {
                    continue;
                }

                CleanupGeneratedEcsObjects(tilemap.gameObject);

                if (tilemap.GetComponent<TilemapCollider2D>() == null)
                {
                    tilemap.gameObject.AddComponent<TilemapCollider2D>();
                }
            }
        }

        private static void CleanupGeneratedEcsObjects(GameObject tilemapObject)
        {
            if (tilemapObject == null)
            {
                return;
            }

            for (var i = tilemapObject.transform.childCount - 1; i >= 0; i--)
            {
                var child = tilemapObject.transform.GetChild(i);
                if (child == null || !child.name.StartsWith(EcsPhysicsProvider.IslandColliderObjectPrefix))
                {
                    continue;
                }

                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}