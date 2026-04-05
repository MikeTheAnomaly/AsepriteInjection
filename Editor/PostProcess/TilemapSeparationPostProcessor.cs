using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class TilemapSeparationPostProcessor
    {
        public static List<Tilemap> Run(AsepriteImporter.ImportEventArgs args)
        {
            var importedObjects = new List<UnityEngine.Object>();
            args.context.GetObjects(importedObjects);
            var generatedTilemaps = new List<Tilemap>();

            var settings = AsepriteImportSettingsStorage.GetSettings(args.importer) ?? new AsepriteInjectionSettings();
            var overrideMaterial = AsepriteImportSettingsStorage.ResolveOverrideMaterial(settings);

            var importedTilemaps = new List<Tilemap>();
            importedTilemaps.AddRange(importedObjects.OfType<Tilemap>());

            foreach (var go in importedObjects.OfType<GameObject>())
            {
                if (go != null)
                {
                    importedTilemaps.AddRange(go.GetComponentsInChildren<Tilemap>(true));
                }
            }

            if (importedTilemaps.Count == 0)
            {
                return generatedTilemaps;
            }

            foreach (var sourceTilemap in importedTilemaps)
            {
                if (sourceTilemap == null)
                {
                    continue;
                }

                SeparateTilemap(args, settings, overrideMaterial, sourceTilemap, generatedTilemaps);
            }

            return generatedTilemaps;
        }

        private static void SeparateTilemap(
            AsepriteImporter.ImportEventArgs args,
            AsepriteInjectionSettings settings,
            Material overrideMaterial,
            Tilemap sourceTilemap,
            List<Tilemap> generatedTilemaps)
        {
            var bounds = sourceTilemap.cellBounds;
            var parentTransform = sourceTilemap.gameObject.transform.parent ?? sourceTilemap.gameObject.transform;
            var layersPerChunk = Mathf.Max(1, settings.tilemapLayersPerChunk);

            GameObject currentRoom = null;
            var createdLayersInChunk = 0;
            var chunkCount = 0;

            for (var z = bounds.zMin; z <= bounds.zMax; z++)
            {
                var layerBounds = new BoundsInt(bounds.xMin, bounds.yMin, z, bounds.size.x, bounds.size.y, 1);
                var tiles = sourceTilemap.GetTilesBlock(layerBounds);
                if (!ContainsAnyTiles(tiles))
                {
                    continue;
                }

                var layerGameObject = new GameObject(sourceTilemap.name + "_layer_" + z);

                if (layersPerChunk > 1)
                {
                    if (currentRoom == null || createdLayersInChunk >= layersPerChunk)
                    {
                        chunkCount++;
                        currentRoom = CreateOrUpdateChunkParent(args, settings, parentTransform, chunkCount);
                        createdLayersInChunk = 0;
                    }

                    if (settings.roomNames != null && settings.roomNames.Length >= chunkCount)
                    {
                        layerGameObject.name = settings.roomNames[chunkCount - 1] + " layer " + z;
                    }

                    layerGameObject.transform.SetParent(currentRoom.transform, false);
                    createdLayersInChunk++;
                }
                else if (parentTransform != null)
                {
                    layerGameObject.transform.SetParent(parentTransform, false);
                }

                var newTilemap = layerGameObject.AddComponent<Tilemap>();
                var newRenderer = layerGameObject.AddComponent<TilemapRenderer>();
                generatedTilemaps.Add(newTilemap);

                TryAddComponentByName(layerGameObject, "TilemapBake");
                ConfigureTilemapBakeDefaults(layerGameObject.GetComponent("TilemapBake"));

                newTilemap.tileAnchor = sourceTilemap.tileAnchor;
                newTilemap.orientation = sourceTilemap.orientation;
                newTilemap.color = sourceTilemap.color;
                newRenderer.sortingOrder = z;

                if (overrideMaterial != null)
                {
                    newRenderer.sharedMaterial = overrideMaterial;
                }

                CopyLayerTiles(sourceTilemap, newTilemap, layerBounds, tiles);

                try
                {
                    args.context.AddObjectToAsset(layerGameObject.name, layerGameObject);
                }
                catch (Exception)
                {
                    // Ignore contexts that do not support adding GameObjects as sub-assets.
                }
            }
        }

        private static bool ContainsAnyTiles(TileBase[] tiles)
        {
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject CreateOrUpdateChunkParent(
            AsepriteImporter.ImportEventArgs args,
            AsepriteInjectionSettings settings,
            Transform parentTransform,
            int chunkCount)
        {
            var chunkName = "room chunk " + chunkCount;
            if (settings.roomNames != null && settings.roomNames.Length >= chunkCount)
            {
                chunkName = "room " + settings.roomNames[chunkCount - 1] + " " + chunkCount;
            }

            var chunk = new GameObject(chunkName);
            TryAddComponentByName(chunk, "FadeController");

            if (parentTransform != null)
            {
                chunk.transform.SetParent(parentTransform, false);
            }

            try
            {
                args.context.AddObjectToAsset(chunk.name, chunk);
            }
            catch (Exception)
            {
                // Ignore contexts that do not support adding GameObjects as sub-assets.
            }

            return chunk;
        }

        private static void CopyLayerTiles(Tilemap sourceTilemap, Tilemap targetTilemap, BoundsInt layerBounds, TileBase[] tiles)
        {
            var index = 0;
            foreach (var sourcePos in layerBounds.allPositionsWithin)
            {
                var tile = tiles[index++];
                if (tile == null)
                {
                    continue;
                }

                var targetPos = new Vector3Int(sourcePos.x, sourcePos.y, 0);
                targetTilemap.SetTile(targetPos, tile);
                targetTilemap.SetColor(targetPos, sourceTilemap.GetColor(sourcePos));
                targetTilemap.SetTransformMatrix(targetPos, sourceTilemap.GetTransformMatrix(sourcePos));
            }
        }

        private static void ConfigureTilemapBakeDefaults(Component tilemapBake)
        {
            if (tilemapBake == null)
            {
                return;
            }

            var bakeTypeField = tilemapBake.GetType().GetField("bakeType", BindingFlags.Public | BindingFlags.Instance);
            if (bakeTypeField != null && bakeTypeField.FieldType.IsEnum)
            {
                try
                {
                    var wallsValue = Enum.Parse(bakeTypeField.FieldType, "Walls", ignoreCase: true);
                    bakeTypeField.SetValue(tilemapBake, wallsValue);
                }
                catch (ArgumentException)
                {
                    // Ignore enum value mismatches.
                }
            }

            var maxHeightField = tilemapBake.GetType().GetField("maxWallTilesHeight", BindingFlags.Public | BindingFlags.Instance);
            if (maxHeightField != null && maxHeightField.FieldType == typeof(int))
            {
                maxHeightField.SetValue(tilemapBake, 4);
            }
        }

        private static Component TryAddComponentByName(GameObject gameObject, string typeName)
        {
            if (gameObject == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var existing = gameObject.GetComponent(typeName);
            if (existing != null)
            {
                return existing;
            }

            var targetType = FindTypeByName(typeName);
            if (targetType == null || !typeof(Component).IsAssignableFrom(targetType))
            {
                return null;
            }

            return gameObject.AddComponent(targetType);
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type foundType;

                try
                {
                    foundType = assembly.GetTypes().FirstOrDefault(type => type.Name == typeName);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    foundType = ex.Types?.FirstOrDefault(type => type != null && type.Name == typeName);
                }

                if (foundType != null)
                {
                    return foundType;
                }
            }

            return null;
        }
    }
}
