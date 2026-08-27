using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Geometry;
using SpatialTrees;

namespace BenchMarks
{
    /// <summary>
    /// Non-destructive Octree benchmarks: bulk build, and repeated cube / sphere collision
    /// queries against a pre-built tree. Mirrors <see cref="QuadtreeBenchmarks"/> one
    /// dimension up.
    /// </summary>
    [MemoryDiagnoser]
    public class OctreeBenchmarks
    {
        public const int MaxDepth = 8;
        public const int MaxObjects = 16;

        public const int QueryCount = 1_000;
        public const float QueryBoxSize = 200f;
        public const float QueryRadius = 100f;

        [Params(1_000, 10_000, 50_000)]
        public int ItemCount;

        private Point3[] _positions;
        private BenchItem3d[] _items;
        private Cube[] _cubeQueries;
        private Sphere[] _sphereQueries;
        private Octree _prebuilt;
        private List<IMapObject3d> _results;

        [GlobalSetup]
        public void Setup()
        {
            _positions = WorldData.Points3d(ItemCount);
            _items = new BenchItem3d[ItemCount];

            for (int i = 0; i < ItemCount; i++)
                _items[i] = new BenchItem3d(_positions[i].X, _positions[i].Y, _positions[i].Z);

            _cubeQueries = WorldData.QueryCubes(QueryCount, QueryBoxSize);
            _sphereQueries = WorldData.QuerySpheres(QueryCount, QueryRadius);
            _results = new List<IMapObject3d>();

            _prebuilt = BuildTree();
        }

        public Octree BuildTree()
        {
            var tree = new Octree(WorldData.World3d(), MaxDepth, MaxObjects, expectedItemCount: ItemCount);

            foreach (var item in _items)
                tree.AddItem(item);

            return tree;
        }

        [Benchmark]
        public Octree Build()
        {
            return BuildTree();
        }

        [Benchmark]
        public int QueryCube()
        {
            int hits = 0;
            var results = _results;

            foreach (var query in _cubeQueries)
            {
                _prebuilt.GetCollidingItems(query, WorldData.AllTypes, ref results);
                hits += results.Count;
            }

            return hits;
        }

        [Benchmark]
        public int QuerySphere()
        {
            int hits = 0;
            var results = _results;

            foreach (var query in _sphereQueries)
            {
                _prebuilt.GetCollidingItems(query, WorldData.AllTypes, ref results);
                hits += results.Count;
            }

            return hits;
        }
    }
}
