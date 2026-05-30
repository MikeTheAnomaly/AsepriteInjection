using System.Collections.Generic;
using System;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class EcsPhysicsProvider
    {
        internal const string IslandColliderObjectPrefix = "_PhysicsIsland_";
        private const float MeshHeight = 1f;

        private struct CellFootprint
        {
            public float minX;
            public float maxX;
            public float minY;
            public float maxY;
        }

        private struct IslandEdgeOffsets
        {
            public float westX;
            public float eastX;
            public float southY;
            public float northY;
        }

        public static void Apply(AsepriteImporter.ImportEventArgs args, List<Tilemap> tilemaps)
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

                var tilemapObject = tilemap.gameObject;
                CleanupGeneratedIslandColliders(tilemapObject);

                var tilemapCollider2D = tilemap.GetComponent<TilemapCollider2D>();
                if (tilemapCollider2D != null)
                {
                    UnityEngine.Object.DestroyImmediate(tilemapCollider2D);
                }

                var compositeCollider2D = tilemap.GetComponent<CompositeCollider2D>();
                if (compositeCollider2D != null)
                {
                    UnityEngine.Object.DestroyImmediate(compositeCollider2D);
                }

                var rigidbody2D = tilemap.GetComponent<Rigidbody2D>();
                if (rigidbody2D != null && rigidbody2D.bodyType == RigidbodyType2D.Static && tilemap.GetComponents<Collider2D>().Length == 0)
                {
                    UnityEngine.Object.DestroyImmediate(rigidbody2D);
                }

                var islands = FindTileIslands(tilemap);
                var islandIndex = 1;
                foreach (var island in islands)
                {
                    var mesh = CreateIslandMesh(tilemap, island, MeshHeight);
                    if (mesh == null)
                    {
                        continue;
                    }

                    var islandObject = new GameObject(IslandColliderObjectPrefix + islandIndex);
                    var meshName = tilemap.name + "_PhysicsIslandMesh_" + islandIndex;
                    islandIndex++;

                    islandObject.transform.SetParent(tilemapObject.transform, false);
                    islandObject.isStatic = true;

                    mesh.name = meshName;
                    TryAddObjectToAsset(args, meshName, mesh);

                    var meshCollider = islandObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = mesh;
                }

                tilemapObject.isStatic = true;
            }
        }

        private static void CleanupGeneratedIslandColliders(GameObject tilemapObject)
        {
            if (tilemapObject == null)
            {
                return;
            }

            for (var i = tilemapObject.transform.childCount - 1; i >= 0; i--)
            {
                var child = tilemapObject.transform.GetChild(i);
                if (child == null || !child.name.StartsWith(IslandColliderObjectPrefix))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static List<List<Vector3Int>> FindTileIslands(Tilemap tilemap)
        {
            var occupied = new HashSet<Vector3Int>();
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(pos) != null)
                {
                    occupied.Add(pos);
                }
            }

            var result = new List<List<Vector3Int>>();
            var visited = new HashSet<Vector3Int>();

            foreach (var cell in occupied)
            {
                if (visited.Contains(cell))
                {
                    continue;
                }

                var island = new List<Vector3Int>();
                var queue = new Queue<Vector3Int>();
                queue.Enqueue(cell);
                visited.Add(cell);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    island.Add(current);

                    var right = new Vector3Int(current.x + 1, current.y, current.z);
                    var left = new Vector3Int(current.x - 1, current.y, current.z);
                    var up = new Vector3Int(current.x, current.y + 1, current.z);
                    var down = new Vector3Int(current.x, current.y - 1, current.z);

                    TryEnqueueNeighbor(right, occupied, visited, queue);
                    TryEnqueueNeighbor(left, occupied, visited, queue);
                    TryEnqueueNeighbor(up, occupied, visited, queue);
                    TryEnqueueNeighbor(down, occupied, visited, queue);
                }

                if (island.Count > 0)
                {
                    result.Add(island);
                }
            }

            return result;
        }

        private static void TryEnqueueNeighbor(Vector3Int candidate, HashSet<Vector3Int> occupied, HashSet<Vector3Int> visited, Queue<Vector3Int> queue)
        {
            if (!occupied.Contains(candidate) || visited.Contains(candidate))
            {
                return;
            }

            visited.Add(candidate);
            queue.Enqueue(candidate);
        }

        private static Mesh CreateIslandMesh(Tilemap tilemap, List<Vector3Int> island, float height)
        {
            if (tilemap == null || island == null || island.Count == 0)
            {
                return null;
            }

            var islandSet = new HashSet<Vector3Int>(island);
            var vertices = new List<Vector3>(island.Count * 24);
            var triangles = new List<int>(island.Count * 36);

            var cellSize = tilemap.layoutGrid.cellSize;
            var halfX = Mathf.Abs(cellSize.x) * 0.5f;
            var halfY = Mathf.Abs(cellSize.y) * 0.5f;
            var halfZ = height * 0.5f;
            var footprints = BuildFootprints(tilemap, island, halfX, halfY);
            var edgeOffsets = ComputeIslandEdgeOffsets(island, islandSet, footprints, halfX, halfY);

            foreach (var cell in island)
            {
                var centerWorld = tilemap.GetCellCenterWorld(cell);
                var centerLocal = tilemap.transform.InverseTransformPoint(centerWorld);
                var footprint = footprints[cell];

                var leftX = centerLocal.x + footprint.minX;
                var rightX = centerLocal.x + footprint.maxX;
                var bottomY = centerLocal.y + footprint.minY;
                var topY = centerLocal.y + footprint.maxY;

                var north = new Vector3Int(cell.x, cell.y + 1, cell.z);
                var south = new Vector3Int(cell.x, cell.y - 1, cell.z);
                var east = new Vector3Int(cell.x + 1, cell.y, cell.z);
                var west = new Vector3Int(cell.x - 1, cell.y, cell.z);

                AddQuad(vertices, triangles,
                    new Vector3(leftX, bottomY, centerLocal.z + halfZ),
                    new Vector3(rightX, bottomY, centerLocal.z + halfZ),
                    new Vector3(rightX, topY, centerLocal.z + halfZ),
                    new Vector3(leftX, topY, centerLocal.z + halfZ));

                AddQuad(vertices, triangles,
                    new Vector3(leftX, topY, centerLocal.z - halfZ),
                    new Vector3(rightX, topY, centerLocal.z - halfZ),
                    new Vector3(rightX, bottomY, centerLocal.z - halfZ),
                    new Vector3(leftX, bottomY, centerLocal.z - halfZ));

                if (!islandSet.Contains(north))
                {
                    var northY = centerLocal.y + edgeOffsets.northY;
                    AddQuad(vertices, triangles,
                        new Vector3(leftX, northY, centerLocal.z - halfZ),
                        new Vector3(rightX, northY, centerLocal.z - halfZ),
                        new Vector3(rightX, northY, centerLocal.z + halfZ),
                        new Vector3(leftX, northY, centerLocal.z + halfZ));
                }

                if (!islandSet.Contains(south))
                {
                    var southY = centerLocal.y + edgeOffsets.southY;
                    AddQuad(vertices, triangles,
                        new Vector3(rightX, southY, centerLocal.z - halfZ),
                        new Vector3(leftX, southY, centerLocal.z - halfZ),
                        new Vector3(leftX, southY, centerLocal.z + halfZ),
                        new Vector3(rightX, southY, centerLocal.z + halfZ));
                }

                if (!islandSet.Contains(east))
                {
                    var eastX = centerLocal.x + edgeOffsets.eastX;
                    AddQuad(vertices, triangles,
                        new Vector3(eastX, topY, centerLocal.z - halfZ),
                        new Vector3(eastX, bottomY, centerLocal.z - halfZ),
                        new Vector3(eastX, bottomY, centerLocal.z + halfZ),
                        new Vector3(eastX, topY, centerLocal.z + halfZ));
                }

                if (!islandSet.Contains(west))
                {
                    var westX = centerLocal.x + edgeOffsets.westX;
                    AddQuad(vertices, triangles,
                        new Vector3(westX, bottomY, centerLocal.z - halfZ),
                        new Vector3(westX, topY, centerLocal.z - halfZ),
                        new Vector3(westX, topY, centerLocal.z + halfZ),
                        new Vector3(westX, bottomY, centerLocal.z + halfZ));
                }
            }

            var mesh = new Mesh { name = tilemap.name + "_IslandPhysicsMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Dictionary<Vector3Int, CellFootprint> BuildFootprints(Tilemap tilemap, List<Vector3Int> island, float defaultHalfX, float defaultHalfY)
        {
            var result = new Dictionary<Vector3Int, CellFootprint>(island.Count);
            foreach (var cell in island)
            {
                result[cell] = GetCellFootprint(tilemap, cell, defaultHalfX, defaultHalfY);
            }

            return result;
        }

        private static CellFootprint GetCellFootprint(Tilemap tilemap, Vector3Int cell, float defaultHalfX, float defaultHalfY)
        {
            var fallback = new CellFootprint
            {
                minX = -defaultHalfX,
                maxX = defaultHalfX,
                minY = -defaultHalfY,
                maxY = defaultHalfY
            };

            var sprite = tilemap.GetSprite(cell);
            if (sprite == null)
            {
                return fallback;
            }

            var bounds = sprite.bounds;
            var tileMatrix = tilemap.GetTransformMatrix(cell);

            var c0 = tileMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y, 0f));
            var c1 = tileMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y, 0f));
            var c2 = tileMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y, 0f));
            var c3 = tileMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y, 0f));

            var minX = Mathf.Min(c0.x, c1.x, c2.x, c3.x);
            var maxX = Mathf.Max(c0.x, c1.x, c2.x, c3.x);
            var minY = Mathf.Min(c0.y, c1.y, c2.y, c3.y);
            var maxY = Mathf.Max(c0.y, c1.y, c2.y, c3.y);

            if (maxX - minX <= Mathf.Epsilon || maxY - minY <= Mathf.Epsilon)
            {
                return fallback;
            }

            return new CellFootprint
            {
                minX = minX,
                maxX = maxX,
                minY = minY,
                maxY = maxY
            };
        }

        private static IslandEdgeOffsets ComputeIslandEdgeOffsets(
            List<Vector3Int> island,
            HashSet<Vector3Int> islandSet,
            Dictionary<Vector3Int, CellFootprint> footprints,
            float defaultHalfX,
            float defaultHalfY)
        {
            var westSum = 0f;
            var eastSum = 0f;
            var southSum = 0f;
            var northSum = 0f;
            var westCount = 0;
            var eastCount = 0;
            var southCount = 0;
            var northCount = 0;

            foreach (var cell in island)
            {
                var footprint = footprints[cell];
                var north = new Vector3Int(cell.x, cell.y + 1, cell.z);
                var south = new Vector3Int(cell.x, cell.y - 1, cell.z);
                var east = new Vector3Int(cell.x + 1, cell.y, cell.z);
                var west = new Vector3Int(cell.x - 1, cell.y, cell.z);

                if (!islandSet.Contains(west))
                {
                    westSum += footprint.minX;
                    westCount++;
                }

                if (!islandSet.Contains(east))
                {
                    eastSum += footprint.maxX;
                    eastCount++;
                }

                if (!islandSet.Contains(south))
                {
                    southSum += footprint.minY;
                    southCount++;
                }

                if (!islandSet.Contains(north))
                {
                    northSum += footprint.maxY;
                    northCount++;
                }
            }

            return new IslandEdgeOffsets
            {
                westX = westCount > 0 ? westSum / westCount : -defaultHalfX,
                eastX = eastCount > 0 ? eastSum / eastCount : defaultHalfX,
                southY = southCount > 0 ? southSum / southCount : -defaultHalfY,
                northY = northCount > 0 ? northSum / northCount : defaultHalfY
            };
        }

        private static void TryAddObjectToAsset(AsepriteImporter.ImportEventArgs args, string identifier, UnityEngine.Object obj)
        {
            if (args.context == null || obj == null)
            {
                return;
            }

            try
            {
                args.context.AddObjectToAsset(identifier, obj);
            }
            catch (Exception)
            {
                // Some import contexts may reject object registration; keep import robust.
            }
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var baseIndex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            triangles.Add(baseIndex + 0);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);

            triangles.Add(baseIndex + 0);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }
    }
}