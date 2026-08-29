using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Geometry;
using SpatialTrees.Octrees;

namespace BenchMarks
{
    /// <summary>
    /// Destructive Octree benchmarks - remove every item, or move every item once.
    /// Same rebuild-per-iteration approach as <see cref="QuadtreeMutationBenchmarks"/>.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Monitoring, iterationCount: 20, warmupCount: 3)]
    public class OctreeMutationBenchmarks
    {
        [Params(50_000)]
        public int ItemCount;

        private Point3[] _positions;
        private BenchItem3d[] _items;
        private Octree _tree;

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
        }

        [Benchmark]
        public void RemoveAll()
        {
            foreach (var item in _items)
                _tree.RemoveItem(item);
        }

        [Benchmark]
        public void MoveAll()
        {
            foreach (var item in _items)
            {
                item.Nudge();
                _tree.MoveItem(item);
            }
        }
    }
}
