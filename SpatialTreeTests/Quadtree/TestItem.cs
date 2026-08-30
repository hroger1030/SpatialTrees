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

using SpatialTrees.Quadtrees;
using Geometry;
using System;

namespace SpatialTreesTests
{
    internal class TestItem : IMapObject2d
    {
        [Flags]
        public enum Properties
        {
            Property1 = 1,
            Property2 = 2,
            Property3 = 4,
            All = ~0,
        }

        public string Name { get; set; }

        public int ObjectType { get; set; }

        public Point2 Location { get; set; }

        // Defaults to a 1x1 box centered on Location; override for items large enough to
        // straddle a quadrant boundary.
        public float Width { get; set; } = 1f;
        public float Height { get; set; } = 1f;

        public Rectangle BoundingBox
        {
            get { return new Rectangle(Location, Width, Height); }
        }

        public TestItem() { }

        public TestItem(string name, float x, float y, int objectTypes)
        {
            Name = name;
            Location = new Point2(x, y);
            ObjectType = objectTypes;
        }

        public TestItem(string name, float x, float y, float width, float height, int objectTypes)
        {
            Name = name;
            Location = new Point2(x, y);
            Width = width;
            Height = height;
            ObjectType = objectTypes;
        }
    }
}
