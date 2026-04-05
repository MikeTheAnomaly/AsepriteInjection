using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D.Aseprite;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class TilemapPivotPostProcessor
    {
        public static void Run(AsepriteImporter.ImportEventArgs args)
        {
            var settings = AsepriteImportSettingsStorage.GetSettings(args.importer);
            if (!settings.enableTilemapPivotAdjustment)
            {
                return;
            }

            var importedObjects = new List<Object>();
            args.context.GetObjects(importedObjects);

            if (!importedObjects.OfType<Tile>().Any())
            {
                return;
            }

            if (args.importer is not ISpriteEditorDataProvider spriteDataProvider)
            {
                return;
            }

            spriteDataProvider.InitSpriteEditorDataProvider();
            var spriteRects = spriteDataProvider.GetSpriteRects();
            if (spriteRects == null || spriteRects.Length == 0)
            {
                return;
            }

            for (var i = 0; i < spriteRects.Length; i++)
            {
                var spriteRect = spriteRects[i];
                spriteRect.alignment = SpriteAlignment.Custom;
                spriteRect.pivot = new Vector2(0.5f, 0f);
                spriteRects[i] = spriteRect;
            }

            spriteDataProvider.SetSpriteRects(spriteRects);
            spriteDataProvider.Apply();
        }
    }
}
