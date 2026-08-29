using Geometry;
using SpatialTrees.Quadtrees;

namespace BenchMarks
{
    /// <summary>
    /// Minimal <see cref="IMapObject2d"/> for the benchmarks. BoundingBox is rebuilt on
    /// every access, mirroring the common caller pattern (see the test project's TestItem)
    /// - which is exactly the allocation behaviour the tree's hot paths have to cope with.
    /// </summary>
    public sealed class BenchItem2d : IMapObject2d
    {
        public int ObjectTypes { get; set; } = 1;

        public Point2 Location { get; set; }

        public float Width { get; set; } = 1f;

        public float Height { get; set; } = 1f;

        public Rectangle BoundingBox => new Rectangle(Location, Width, Height);

        public BenchItem2d(float x, float y)
        {
            Location = new Point2(x, y);
        }

        /// <summary>Shifts the item by a small fixed delta - used by the MoveItem benchmark.</summary>
        public void Nudge()
        {
            Location = new Point2(Location.X + 0.75f, Location.Y + 0.75f);
        }
    }
}
