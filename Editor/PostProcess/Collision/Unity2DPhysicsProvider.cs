using System.Collections.Generic;
using System;
using System.Linq;
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

                var tilemapCollider2D = tilemap.GetComponent<TilemapCollider2D>();
                if (tilemapCollider2D == null)
                {
                    tilemapCollider2D = tilemap.gameObject.AddComponent<TilemapCollider2D>();
                }

                var rigidbody2D = tilemap.GetComponent<Rigidbody2D>();
                if (rigidbody2D == null)
                {
                    rigidbody2D = tilemap.gameObject.AddComponent<Rigidbody2D>();
                }

                var compositeCollider2D = tilemap.GetComponent<CompositeCollider2D>();
                if (compositeCollider2D == null)
                {
                    compositeCollider2D = tilemap.gameObject.AddComponent<CompositeCollider2D>();
                }

                ConfigureTilemapCollider(tilemapCollider2D);
                ConfigureRigidbody(rigidbody2D);
                ConfigureCompositeCollider(compositeCollider2D);
            }
        }

        private static void ConfigureTilemapCollider(TilemapCollider2D tilemapCollider2D)
        {
            if (tilemapCollider2D == null)
            {
                return;
            }

            tilemapCollider2D.extrusionFactor = 0.01f;

            var colliderType = tilemapCollider2D.GetType();
            var compositeOperationProperty = colliderType.GetProperty("compositeOperation");
            if (compositeOperationProperty != null && compositeOperationProperty.PropertyType.IsEnum)
            {
                var mergeValue = Enum.GetNames(compositeOperationProperty.PropertyType)
                    .FirstOrDefault(name => string.Equals(name, "Merge", StringComparison.Ordinal));

                if (!string.IsNullOrEmpty(mergeValue))
                {
                    var enumValue = Enum.Parse(compositeOperationProperty.PropertyType, mergeValue);
                    compositeOperationProperty.SetValue(tilemapCollider2D, enumValue);
                    return;
                }
            }

            var usedByCompositeProperty = colliderType.GetProperty("usedByComposite");
            if (usedByCompositeProperty != null && usedByCompositeProperty.PropertyType == typeof(bool))
            {
                usedByCompositeProperty.SetValue(tilemapCollider2D, true);
            }
        }

        private static void ConfigureRigidbody(Rigidbody2D rigidbody2D)
        {
            if (rigidbody2D == null)
            {
                return;
            }

            rigidbody2D.bodyType = RigidbodyType2D.Static;
            rigidbody2D.simulated = true;
        }

        private static void ConfigureCompositeCollider(CompositeCollider2D compositeCollider2D)
        {
            if (compositeCollider2D == null)
            {
                return;
            }

            compositeCollider2D.geometryType = CompositeCollider2D.GeometryType.Polygons;
            compositeCollider2D.generationType = CompositeCollider2D.GenerationType.Synchronous;
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

                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}