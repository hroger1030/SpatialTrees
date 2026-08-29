using Geometry;
using SpatialTrees.Octrees;

namespace BenchMarks
{
    /// <summary>
    /// Minimal <see cref="IMapObject3d"/> for the benchmarks. As with <see cref="BenchItem2d"/>
    /// the BoundingBox is rebuilt on every access to model the realistic caller.
    /// </summary>
    public sealed class BenchItem3d : IMapObject3d
    {
        public int ObjectTypes { get; set; } = 1;

        public Point3 Location { get; set; }

        public float Width { get; set; } = 1f;

        public float Height { get; set; } = 1f;

        public float Depth { get; set; } = 1f;

        public Cube BoundingBox => new Cube(
            Location.X - Width / 2f, Location.Y - Height / 2f, Location.Z - Depth / 2f,
            Location.X + Width / 2f, Location.Y + Height / 2f, Location.Z + Depth / 2f);

        public BenchItem3d(float x, float y, float z)
        {
            Location = new Point3(x, y, z);
        }

        /// <summary>Shifts the item by a small fixed delta - used by the MoveItem benchmark.</summary>
        public void Nudge()
        {
            Location = new Point3(Location.X + 0.75f, Location.Y + 0.75f, Location.Z + 0.75f);
        }
    }
}
