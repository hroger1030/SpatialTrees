using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using Geometry;
using SpatialTrees.Quadtrees;

namespace BenchMarks
{
    /// <summary>
    /// Destructive Quadtree benchmarks - remove every item, or move every item once.
    /// Each iteration rebuilds the tree and resets item positions in
    /// <see cref="IterationSetup"/>, so the measured body always starts from the same
    /// state. Runs under <see cref="RunStrategy.Monitoring"/> (one invocation per
    /// iteration) because the body mutates shared state and cannot be repeated blindly.
    ///
    /// Each mutation has a plain <see cref="Quadtree"/> variant and a
    /// <see cref="MultiThreadQuadtree"/> variant (suffix <c>Mt</c>), grouped by category
    /// with the plain variant as baseline, so the ratio column is the write-lock overhead
    /// the thread-safe facade adds per mutation.
    /// </summary>
    [MemoryDiagnoser]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [SimpleJob(RunStrategy.Monitoring, iterationCount: 20, warmupCount: 3)]
    public class QuadtreeMutationBenchmarks
    {
        [Params(50_000)]
        public int ItemCount;

        private Point2[] _positions;
        private BenchItem2d[] _items;
        private Quadtree _tree;
        private MultiThreadQuadtree _treeMt;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _positions = WorldData.Points2d(ItemCount);
            _items = new BenchItem2d[ItemCount];

            for (int i = 0; i < ItemCount; i++)
                _items[i] = new BenchItem2d(_positions[i].X, _positions[i].Y);
        }

        [IterationSetup]
        public void IterationSetup()
        {
            for (int i = 0; i < _items.Length; i++)
                _items[i].Location = new Point2(_positions[i].X, _positions[i].Y);

            _tree = new Quadtree(WorldData.World2d(),
                                 QuadtreeBenchmarks.MaxDepth, QuadtreeBenchmarks.MaxObjects);

            foreach (var item in _items)
                _tree.AddItem(item);

            _treeMt?.Dispose();
            _treeMt = new MultiThreadQuadtree(WorldData.World2d(),
                                              QuadtreeBenchmarks.MaxDepth, QuadtreeBenchmarks.MaxObjects);

            foreach (var item in _items)
                _treeMt.AddItem(item);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _treeMt?.Dispose();
        }

        [Benchmark(Baseline = true), BenchmarkCategory("RemoveAll")]
        public void RemoveAll()
        {
            foreach (var item in _items)
                _tree.RemoveItem(item);
        }

        [Benchmark, BenchmarkCategory("RemoveAll")]
        public void RemoveAllMt()
        {
            foreach (var item in _items)
                _treeMt.RemoveItem(item);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("MoveAll")]
        public void MoveAll()
        {
            foreach (var item in _items)
            {
                item.Nudge();
                _tree.MoveItem(item);
            }
        }

        [Benchmark, BenchmarkCategory("MoveAll")]
        public void MoveAllMt()
        {
            foreach (var item in _items)
            {
                item.Nudge();
                _treeMt.MoveItem(item);
            }
        }
    }
}
