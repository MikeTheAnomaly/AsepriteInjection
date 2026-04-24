using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class PhysicsPostProcessor
    {
        public static void Run(AsepriteImporter.ImportEventArgs args, List<Tilemap> generatedTilemaps)
        {
            var tilemaps = ResolveTilemaps(args, generatedTilemaps);
            if (tilemaps.Count == 0)
            {
                return;
            }

            var settings = AsepriteImportSettingsStorage.GetSettings(args.importer) ?? new AsepriteInjectionSettings();
            var target = settings.physicsImportTarget;

            switch (target)
            {
                case PhysicsImportTarget.Disabled:
                    RemoveGeneratedPhysics(tilemaps);
                    break;
                case PhysicsImportTarget.UnityPhysicsEcs:
                    EcsPhysicsProvider.Apply(args, tilemaps);
                    break;
                default:
                    Unity2DPhysicsProvider.Apply(tilemaps);
                    break;
            }
        }

        private static List<Tilemap> ResolveTilemaps(AsepriteImporter.ImportEventArgs args, List<Tilemap> generatedTilemaps)
        {
            var importedObjects = new List<Object>();
            args.context.GetObjects(importedObjects);

            var collected = new List<Tilemap>();
            collected.AddRange(importedObjects.OfType<Tilemap>());

            foreach (var go in importedObjects.OfType<GameObject>())
            {
                if (go == null)
                {
                    continue;
                }

                collected.AddRange(go.GetComponentsInChildren<Tilemap>(true));
            }

            // Prefer active tilemaps from final hierarchy (for island-separated results).
            var filtered = collected
                .Where(tilemap => tilemap != null && tilemap.gameObject.activeSelf)
                .Distinct()
                .ToList();

            if (filtered.Count > 0)
            {
                return filtered;
            }

            return generatedTilemaps == null
                ? new List<Tilemap>()
                : generatedTilemaps.Where(tilemap => tilemap != null).Distinct().ToList();
        }

        private static void RemoveGeneratedPhysics(List<Tilemap> tilemaps)
        {
            foreach (var tilemap in tilemaps)
            {
                if (tilemap == null)
                {
                    continue;
                }

                var tilemapCollider2D = tilemap.GetComponent<TilemapCollider2D>();
                if (tilemapCollider2D != null)
                {
                    Object.DestroyImmediate(tilemapCollider2D);
                }

                for (var i = tilemap.transform.childCount - 1; i >= 0; i--)
                {
                    var child = tilemap.transform.GetChild(i);
                    if (child == null || !child.name.StartsWith(EcsPhysicsProvider.IslandColliderObjectPrefix))
                    {
                        continue;
                    }

                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}