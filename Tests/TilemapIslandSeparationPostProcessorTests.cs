using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection.Tests
{
    public class TilemapIslandSeparationPostProcessorTests
    {
        private const string TestAssetPath = "Assets/AsepriteInjection/Tests/Offset-Test.aseprite";

        [Test]
        public void OffsetTest_CreatesThreeIslands_WithExpectedTileCountsInOrder()
        {
            var importer = AssetImporter.GetAtPath(TestAssetPath);
            Assert.That(importer, Is.Not.Null, $"Could not find importer for {TestAssetPath}");

            var originalUserData = importer.userData;

            try
            {
                EnableIslandSeparation(importer);
                AssetDatabase.ImportAsset(TestAssetPath, ImportAssetOptions.ForceUpdate);

                var islands = GetIslandTilemaps(TestAssetPath);
                Assert.That(islands.Count, Is.EqualTo(3), "Expected exactly 3 generated island tilemaps.");

                var island1Count = CountOccupiedTiles(islands[0]);
                var island2Count = CountOccupiedTiles(islands[1]);
                var island3Count = CountOccupiedTiles(islands[2]);

                Assert.That(island1Count, Is.EqualTo(4), "island_1 tile count mismatch.");
                Assert.That(island2Count, Is.EqualTo(6), "island_2 tile count mismatch.");
                Assert.That(island3Count, Is.EqualTo(3), "island_3 tile count mismatch.");
            }
            finally
            {
                importer.userData = originalUserData;
                EditorUtility.SetDirty(importer);
                AssetDatabase.ImportAsset(TestAssetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        [Test]
        public void OffsetTest_IslandTileAnchors_AreOffsetToIslandLowestPoint()
        {
            var importer = AssetImporter.GetAtPath(TestAssetPath);
            Assert.That(importer, Is.Not.Null, $"Could not find importer for {TestAssetPath}");

            var originalUserData = importer.userData;

            try
            {
                EnableIslandSeparation(importer);
                AssetDatabase.ImportAsset(TestAssetPath, ImportAssetOptions.ForceUpdate);

                var islands = GetIslandTilemaps(TestAssetPath);
                Assert.That(islands.Count, Is.EqualTo(3), "Expected exactly 3 generated island tilemaps.");

                for (var i = 0; i < islands.Count; i++)
                {
                    TestContext.WriteLine($"Island {i + 1}: {DescribeTilemap(islands[i])}");
                }

                var failures = new List<string>();
                ValidateAnchor(islands[0], new Vector3(0.5f, 4f, 0f), "island_1", failures);
                ValidateAnchor(islands[1], new Vector3(0.5f, 2f, 0f), "island_2", failures);
                ValidateAnchor(islands[2], new Vector3(0.5f, 2f, 0f), "island_3", failures);
                ValidateLocalPositionY(islands[0], -3.5f, "island_1", failures);
                ValidateLocalPositionY(islands[1], -1.5f, "island_2", failures);
                ValidateLocalPositionY(islands[2], -1.5f, "island_3", failures);

                Assert.That(
                    failures,
                    Is.Empty,
                    failures.Count == 0 ? string.Empty : string.Join("\n", failures));
            }
            finally
            {
                importer.userData = originalUserData;
                EditorUtility.SetDirty(importer);
                AssetDatabase.ImportAsset(TestAssetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void EnableIslandSeparation(AssetImporter importer)
        {
            var settings = JsonUtility.FromJson<AsepriteInjectionSettings>(importer.userData) ?? new AsepriteInjectionSettings();
            settings.enableTilemapIslandSeparation = true;
            importer.userData = JsonUtility.ToJson(settings);
            EditorUtility.SetDirty(importer);
        }

        private static List<Tilemap> GetIslandTilemaps(string assetPath)
        {
            return AssetDatabase
                .LoadAllAssetsAtPath(assetPath)
                .OfType<GameObject>()
                .Where(go => go.name.Contains("_island_"))
                .OrderBy(go => ExtractIslandIndex(go.name))
                .Select(go => go.GetComponent<Tilemap>())
                .Where(tilemap => tilemap != null)
                .ToList();
        }

        private static int ExtractIslandIndex(string objectName)
        {
            var marker = "_island_";
            var index = objectName.LastIndexOf(marker, System.StringComparison.Ordinal);
            if (index < 0)
            {
                return int.MaxValue;
            }

            var numberPart = objectName.Substring(index + marker.Length);
            return int.TryParse(numberPart, out var parsedIndex) ? parsedIndex : int.MaxValue;
        }

        private static int CountOccupiedTiles(Tilemap tilemap)
        {
            var count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(position))
                {
                    count++;
                }
            }

            return count;
        }

        private static string DescribeTilemap(Tilemap tilemap)
        {
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            var occupiedCount = 0;

            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(position))
                {
                    continue;
                }

                occupiedCount++;
                if (position.y < minY)
                {
                    minY = position.y;
                }

                if (position.y > maxY)
                {
                    maxY = position.y;
                }
            }

            var yRange = occupiedCount == 0 ? "none" : $"[{minY}..{maxY}]";
            return $"name={tilemap.name}, tileAnchor={tilemap.tileAnchor}, origin={tilemap.origin}, size={tilemap.size}, occupied={occupiedCount}, occupiedY={yRange}";
        }

        private static void ValidateAnchor(Tilemap tilemap, Vector3 expectedAnchor, string islandName, List<string> failures)
        {
            if (tilemap.tileAnchor != expectedAnchor)
            {
                failures.Add($"{islandName} tile anchor mismatch. Expected={expectedAnchor}, Actual={tilemap.tileAnchor}. {DescribeTilemap(tilemap)}");
            }
        }

        private static void ValidateLocalPositionY(Tilemap tilemap, float expectedY, string islandName, List<string> failures)
        {
            var actualY = tilemap.transform.localPosition.y;
            if (!Mathf.Approximately(actualY, expectedY))
            {
                failures.Add($"{islandName} localPosition.y mismatch. Expected={expectedY}, Actual={actualY}. {DescribeTilemap(tilemap)}");
            }
        }
    }
}
