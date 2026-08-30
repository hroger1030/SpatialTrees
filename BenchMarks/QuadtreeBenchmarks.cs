using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Geometry;
using SpatialTrees.Quadtrees;

namespace BenchMarks
{
    /// <summary>
    /// Non-destructive Quadtree benchmarks: bulk build, and repeated rectangle / circle
    /// collision queries against a pre-built tree. <see cref="MemoryDiagnoserAttribute"/>
    /// reports allocations, which is the main thing we are chasing.
    ///
    /// Every operation has a plain <see cref="Quadtree"/> variant and a
    /// <see cref="MultiThreadQuadtree"/> variant (suffix <c>Mt</c>), grouped into a
    /// benchmark category so the ratio column shows the cost of the thread-safe facade's
    /// <see cref="System.Threading.ReaderWriterLockSlim"/> directly against its
    /// single-threaded counterpart (the plain variant is the category baseline). These are
    /// single-threaded calls, so the delta is pure lock overhead - the facade's payoff
    /// (parallel reads) is not what this class measures.
    /// </summary>
    [MemoryDiagnoser]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
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
        private MultiThreadQuadtree _prebuiltMt;
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
            _prebuiltMt = new MultiThreadQuadtree(BuildTree());
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _prebuiltMt?.Dispose();
        }

        public Quadtree BuildTree()
        {
            var tree = new Quadtree(WorldData.World2d(), MaxDepth, MaxObjects, expectedItemCount: ItemCount);

            foreach (var item in _items)
                tree.AddItem(item);

            return tree;
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Build")]
        public Quadtree Build()
        {
            return BuildTree();
        }

        [Benchmark, BenchmarkCategory("Build")]
        public MultiThreadQuadtree BuildMt()
        {
            var tree = new MultiThreadQuadtree(WorldData.World2d(), MaxDepth, MaxObjects, expectedItemCount: ItemCount);

            foreach (var item in _items)
                tree.AddItem(item);

            return tree;
        }

        [Benchmark(Baseline = true), BenchmarkCategory("BuildBulk")]
        public Quadtree BuildBulk()
        {
            return Quadtree.Build(WorldData.World2d(), MaxDepth, MaxObjects, _items);
        }

        [Benchmark, BenchmarkCategory("BuildBulk")]
        public MultiThreadQuadtree BuildBulkMt()
        {
            return MultiThreadQuadtree.Build(WorldData.World2d(), MaxDepth, MaxObjects, _items);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("QueryRectangle")]
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

        [Benchmark, BenchmarkCategory("QueryRectangle")]
        public int QueryRectangleMt()
        {
            int hits = 0;
            var results = _results;

            foreach (var query in _rectQueries)
            {
                _prebuiltMt.GetCollidingItems(query, WorldData.AllTypes, ref results);
                hits += results.Count;
            }

            return hits;
        }

        [Benchmark(Baseline = true), BenchmarkCategory("QueryCircle")]
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

        [Benchmark, BenchmarkCategory("QueryCircle")]
        public int QueryCircleMt()
        {
            int hits = 0;
            var results = _results;

            foreach (var query in _circleQueries)
            {
                _prebuiltMt.GetCollidingItems(query, WorldData.AllTypes, ref results);
                hits += results.Count;
            }

            return hits;
        }
    }
}
