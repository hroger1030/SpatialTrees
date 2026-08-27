using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Geometry;
using SpatialTrees;

namespace BenchMarks
{
    /// <summary>
    /// Non-destructive Quadtree benchmarks: bulk build, and repeated rectangle / circle
    /// collision queries against a pre-built tree. <see cref="MemoryDiagnoserAttribute"/>
    /// reports allocations, which is the main thing we are chasing.
    /// </summary>
    [MemoryDiagnoser]
    public class QuadtreeBenchmarks
    {
        // Tree tuning is itself a perf knob; fixed here so item count is the only variable.
        // Change these to explore the split heuristic.
        public const int MaxDepth = 8;
        public const int MaxObjects = 16;

        public const int QueryCount = 1_000;
        public const float QueryBoxSize = 100f;
        public const float QueryRadius = 50f;

        [Params(1_000, 10_000, 50_000)]
        public int ItemCount;

        private Point2[] _positions;
        private BenchItem2d[] _items;
        private Rectangle[] _rectQueries;
        private Circle[] _circleQueries;
        private Quadtree _prebuilt;
        private List<IMapObject2d> _results;

        [GlobalSetup]
        public void Setup()
        {
            _positions = WorldData.Points2d(ItemCount);
            _items = new BenchItem2d[ItemCount];

            for (int i = 0; i < ItemCount; i++)
                _items[i] = new BenchItem2d(_positions[i].X, _positions[i].Y);

            _rectQueries = WorldData.QueryRects(QueryCount, QueryBoxSize);
            _circleQueries = WorldData.QueryCircles(QueryCount, QueryRadius);
            _results = new List<IMapObject2d>();

            _prebuilt = BuildTree();
        }

        public Quadtree BuildTree()
        {
            var tree = new Quadtree(WorldData.World2d(), MaxDepth, MaxObjects, expectedItemCount: ItemCount);

            foreach (var item in _items)
                tree.AddItem(item);

            return tree;
        }

        [Benchmark]
        public Quadtree Build()
        {
            return BuildTree();
        }

        [Benchmark]
        public Quadtree BuildBulk()
        {
            return Quadtree.Build(WorldData.World2d(), MaxDepth, MaxObjects, _items);
        }

        [Benchmark]
        public int QueryRectangle()
        {
            int hits = 0;
            var results = _results;

            foreach (var query in _rectQueries)
            {
                _prebuilt.GetCollidingItems(query, WorldData.AllTypes, ref results);
                hits += results.Count;
            }

            return hits;
        }

        [Benchmark]
        public int QueryCircle()
        {
            int hits = 0;
            var results = _results;

            foreach (var query in _circleQueries)
            {
                _prebuilt.GetCollidingItems(query, WorldData.AllTypes, ref results);
                hits += results.Count;
            }

            return hits;
        }
    }
}
