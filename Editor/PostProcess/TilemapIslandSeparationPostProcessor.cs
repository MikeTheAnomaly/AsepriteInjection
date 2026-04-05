using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class TilemapIslandSeparationPostProcessor
    {
        public static void Run(AsepriteImporter.ImportEventArgs args, List<Tilemap> generatedTilemaps)
        {
            if (generatedTilemaps == null || generatedTilemaps.Count == 0)
            {
                return;
            }

            var settings = AsepriteImportSettingsStorage.GetSettings(args.importer) ?? new AsepriteInjectionSettings();
            if (!settings.enableTilemapIslandSeparation)
            {
                return;
            }

            foreach (var sourceTilemap in generatedTilemaps.Distinct())
            {
                if (sourceTilemap == null)
                {
                    continue;
                }

                SplitTilemapIslands(args, sourceTilemap);
            }
        }

        private static void SplitTilemapIslands(AsepriteImporter.ImportEventArgs args, Tilemap sourceTilemap)
        {
            var occupiedPositions = GetOccupiedPositions(sourceTilemap);
            if (occupiedPositions.Count <= 1)
            {
                return;
            }

            var islands = FindIslands(occupiedPositions);
            if (islands.Count <= 1)
            {
                return;
            }

            var sourceRenderer = sourceTilemap.GetComponent<TilemapRenderer>();
            var parent = sourceTilemap.transform.parent;

            var islandIndex = 1;
            foreach (var island in islands)
            {
                var islandObject = new GameObject(sourceTilemap.name + "_island_" + islandIndex);
                islandIndex++;

                if (parent != null)
                {
                    islandObject.transform.SetParent(parent, false);
                }

                var islandTilemap = islandObject.AddComponent<Tilemap>();
                var islandRenderer = islandObject.AddComponent<TilemapRenderer>();

                CopyTilemapSettings(sourceTilemap, islandTilemap);
                CopyRendererSettings(sourceRenderer, islandRenderer);
                CopyTilesForIsland(sourceTilemap, islandTilemap, island);

                CopyComponentByTypeName(sourceTilemap.gameObject, islandObject, "TilemapBake");

                try
                {
                    args.context.AddObjectToAsset(islandObject.name, islandObject);
                }
                catch (Exception)
                {
                    // Ignore import contexts that do not support GameObject sub-assets.
                }
            }

            sourceTilemap.gameObject.SetActive(false);
        }

        private static HashSet<Vector3Int> GetOccupiedPositions(Tilemap tilemap)
        {
            var positions = new HashSet<Vector3Int>();
            var bounds = tilemap.cellBounds;

            foreach (var pos in bounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(pos);
                if (tile != null)
                {
                    positions.Add(pos);
                }
            }

            return positions;
        }

        private static List<List<Vector3Int>> FindIslands(HashSet<Vector3Int> occupiedPositions)
        {
            var islands = new List<List<Vector3Int>>();
            var visited = new HashSet<Vector3Int>();

            foreach (var position in occupiedPositions)
            {
                if (visited.Contains(position))
                {
                    continue;
                }

                var island = new List<Vector3Int>();
                var queue = new Queue<Vector3Int>();
                queue.Enqueue(position);
                visited.Add(position);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    island.Add(current);

                    foreach (var neighbor in GetNeighbors(current))
                    {
                        if (!occupiedPositions.Contains(neighbor) || visited.Contains(neighbor))
                        {
                            continue;
                        }

                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                islands.Add(island);
            }

            return islands;
        }

        private static IEnumerable<Vector3Int> GetNeighbors(Vector3Int position)
        {
            yield return new Vector3Int(position.x + 1, position.y, position.z);
            yield return new Vector3Int(position.x - 1, position.y, position.z);
            yield return new Vector3Int(position.x, position.y + 1, position.z);
            yield return new Vector3Int(position.x, position.y - 1, position.z);
        }

        private static void CopyTilemapSettings(Tilemap sourceTilemap, Tilemap targetTilemap)
        {
            targetTilemap.tileAnchor = sourceTilemap.tileAnchor;
            targetTilemap.orientation = sourceTilemap.orientation;
            targetTilemap.orientationMatrix = sourceTilemap.orientationMatrix;
            targetTilemap.color = sourceTilemap.color;
            targetTilemap.animationFrameRate = sourceTilemap.animationFrameRate;
        }

        private static void CopyRendererSettings(TilemapRenderer sourceRenderer, TilemapRenderer targetRenderer)
        {
            if (sourceRenderer == null)
            {
                return;
            }

            targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            targetRenderer.sortingOrder = sourceRenderer.sortingOrder;
            targetRenderer.mode = sourceRenderer.mode;
            targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            targetRenderer.maskInteraction = sourceRenderer.maskInteraction;
            targetRenderer.receiveShadows = sourceRenderer.receiveShadows;
            targetRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            targetRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
            targetRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            targetRenderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;
        }

        private static void CopyTilesForIsland(Tilemap sourceTilemap, Tilemap targetTilemap, List<Vector3Int> positions)
        {
            foreach (var sourcePos in positions)
            {
                var targetPos = new Vector3Int(sourcePos.x, sourcePos.y, sourcePos.z);
                var tile = sourceTilemap.GetTile(sourcePos);
                if (tile == null)
                {
                    continue;
                }

                targetTilemap.SetTile(targetPos, tile);
                targetTilemap.SetColor(targetPos, sourceTilemap.GetColor(sourcePos));
                targetTilemap.SetTransformMatrix(targetPos, sourceTilemap.GetTransformMatrix(sourcePos));
                targetTilemap.SetTileFlags(targetPos, sourceTilemap.GetTileFlags(sourcePos));
            }
        }

        private static void CopyComponentByTypeName(GameObject source, GameObject destination, string typeName)
        {
            if (source == null || destination == null || string.IsNullOrEmpty(typeName))
            {
                return;
            }

            var sourceComponent = source.GetComponent(typeName);
            if (sourceComponent == null)
            {
                return;
            }

            var copiedComponent = destination.AddComponent(sourceComponent.GetType());
            EditorUtility.CopySerialized(sourceComponent, copiedComponent);
        }
    }
}
