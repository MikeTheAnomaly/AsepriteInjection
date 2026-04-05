using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class TilemapSortingLayerPostProcessor
    {
        public static void Run(AsepriteImporter.ImportEventArgs args)
        {
            var settings = AsepriteImportSettingsStorage.GetSettings(args.importer) ?? new AsepriteInjectionSettings();
            if (!settings.enableTilemapSortingLayerOverride || string.IsNullOrEmpty(settings.tilemapSortingLayerName))
            {
                return;
            }

            var sortingLayers = SortingLayer.layers;
            var sortingLayer = sortingLayers.FirstOrDefault(layer => layer.name == settings.tilemapSortingLayerName);
            if (string.IsNullOrEmpty(sortingLayer.name))
            {
                return;
            }

            var importedObjects = new List<Object>();
            args.context.GetObjects(importedObjects);

            var renderers = new List<TilemapRenderer>();
            renderers.AddRange(importedObjects.OfType<TilemapRenderer>());
            foreach (var gameObject in importedObjects.OfType<GameObject>())
            {
                if (gameObject == null)
                {
                    continue;
                }

                renderers.AddRange(gameObject.GetComponentsInChildren<TilemapRenderer>(true));
            }

            foreach (var renderer in renderers.Distinct())
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingLayerID = sortingLayer.id;
            }
        }
    }
}
