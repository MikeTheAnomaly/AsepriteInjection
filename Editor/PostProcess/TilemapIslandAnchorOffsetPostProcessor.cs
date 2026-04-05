using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class TilemapIslandAnchorOffsetPostProcessor
    {
        public static void Run(AsepriteImporter.ImportEventArgs args)
        {
            var importedObjects = new List<Object>();
            args.context.GetObjects(importedObjects);

            var settings = AsepriteImportSettingsStorage.GetSettings(args.importer) ?? new AsepriteInjectionSettings();
            if (!settings.enableTilemapIslandSeparation)
            {
                return;
            }

            var islandTilemaps = importedObjects
                .OfType<GameObject>()
                .Where(go => go != null && go.name.Contains("_island_"))
                .Select(go => go.GetComponent<Tilemap>())
                .Where(tilemap => tilemap != null)
                .ToList();

            foreach (var islandTilemap in islandTilemaps)
            {
                var hasTiles = HasAnyTile(islandTilemap);
                if (!hasTiles)
                {
                    continue;
                }

                var currentAnchor = islandTilemap.tileAnchor;
                // After island splitting, tilemap local space is centered by bounds height.
                // Offset Y anchor by half the tilemap height so anchor lands at island bottom.
                var bottomOffset = islandTilemap.size.y * 0.5f;
                var targetAnchorY = currentAnchor.y + bottomOffset;
                var deltaAnchorY = targetAnchorY - currentAnchor.y;

                islandTilemap.tileAnchor = new Vector3(currentAnchor.x, targetAnchorY, currentAnchor.z);

                var currentLocalPosition = islandTilemap.transform.localPosition;
                islandTilemap.transform.localPosition = new Vector3(
                    currentLocalPosition.x,
                    currentLocalPosition.y - deltaAnchorY,
                    currentLocalPosition.z);
            }
        }

        private static bool HasAnyTile(Tilemap tilemap)
        {
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(position))
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
