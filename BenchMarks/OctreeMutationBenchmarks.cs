using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using Geometry;
using SpatialTrees.Octrees;

namespace BenchMarks
{
    /// <summary>
    /// Destructive Octree benchmarks - remove every item, or move every item once.
    /// Same rebuild-per-iteration approach as <see cref="QuadtreeMutationBenchmarks"/>,
    /// including the plain vs <see cref="MultiThreadOctree"/> (<c>Mt</c> suffix) category
    /// pairs that measure the write-lock overhead the thread-safe facade adds per mutation.
    /// </summary>
    [MemoryDiagnoser]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [SimpleJob(RunStrategy.Monitoring, iterationCount: 20, warmupCount: 3)]
    public class OctreeMutationBenchmarks
    {
        [Params(50_000)]
        public int ItemCount;

        private Point3[] _positions;
        private BenchItem3d[] _items;
        private Octree _tree;
        private MultiThreadOctree _treeMt;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _positions = WorldData.Points3d(ItemCount);
            _items = new BenchItem3d[ItemCount];

            for (int i = 0; i < ItemCount; i++)
                _items[i] = new BenchItem3d(_positions[i].X, _positions[i].Y, _positions[i].Z);
        }

        [IterationSetup]
        public void IterationSetup()
        {
            for (int i = 0; i < _items.Length; i++)
                _items[i].Location = new Point3(_positions[i].X, _positions[i].Y, _positions[i].Z);

            _tree = new Octree(WorldData.World3d(),
                               OctreeBenchmarks.MaxDepth, OctreeBenchmarks.MaxObjects);

            foreach (var item in _items)
                _tree.AddItem(item);

            _treeMt?.Dispose();
            _treeMt = new MultiThreadOctree(WorldData.World3d(),
                                            OctreeBenchmarks.MaxDepth, OctreeBenchmarks.MaxObjects);

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
