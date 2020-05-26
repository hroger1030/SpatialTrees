using SpatialTrees;
using Geometry;
using System;

namespace SpatialTreesTests
{
    internal class TestItem : IMapObject
    {
        [Flags]
        public enum Properties
        {
            Property1 = 1,
            Property2 = 2,
            Property3 = 4,
            All = int.MaxValue,
        }

        public string Name { get; set; }

        public int ObjectTypes { get; set; }

        public Point2 Location { get; set; }

        public Rectangle BoundingBox
        {
            get { return new Rectangle(Location, 1f, 1f); }
        }

        public TestItem() { }
    }
}
