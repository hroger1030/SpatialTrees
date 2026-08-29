/*
The MIT License (MIT)

Copyright (c) 2017 Roger Hill

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files
(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using SpatialTrees.Octrees;
using Geometry;
using System;

namespace SpatialTreesTests
{
    internal class TestVolumeItem : IMapObject3d
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

        public Point3 Location { get; set; }

        // Defaults to a 1x1x1 box centered on Location; override for items large enough to
        // straddle an octant boundary.
        public float Width { get; set; } = 1f;
        public float Height { get; set; } = 1f;
        public float Depth { get; set; } = 1f;

        public Cube BoundingBox
        {
            get
            {
                return new Cube(
                    Location.X - Width / 2f, Location.Y - Height / 2f, Location.Z - Depth / 2f,
                    Location.X + Width / 2f, Location.Y + Height / 2f, Location.Z + Depth / 2f);
            }
        }

        public TestVolumeItem() { }

        public TestVolumeItem(string name, float x, float y, float z, int objectTypes)
        {
            Name = name;
            Location = new Point3(x, y, z);
            ObjectTypes = objectTypes;
        }

        public TestVolumeItem(string name, float x, float y, float z, float width, float height, float depth, int objectTypes)
        {
            Name = name;
            Location = new Point3(x, y, z);
            Width = width;
            Height = height;
            Depth = depth;
            ObjectTypes = objectTypes;
        }
    }
}
