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

using FastG;
using SpatialTrees.Octrees;

namespace BenchMarks
{
    /// <summary>
    /// Minimal <see cref="IMapObject3d"/> for the benchmarks. As with <see cref="BenchItem2d"/>
    /// the BoundingBox is rebuilt on every access to model the realistic caller.
    /// </summary>
    public sealed class BenchItem3d : IMapObject3d
    {
        public int ObjectType { get; set; } = 1;

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
