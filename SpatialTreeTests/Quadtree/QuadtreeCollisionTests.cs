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

using Geometry;
using NUnit.Framework;
using SpatialTrees;
using System.Collections.Generic;

namespace SpatialTreesTests
{
    // Single-item fixture with hand-picked geometry so the "tangent" cases touch the item's
    // bounding box at exactly zero area / exactly at the radius, rather than genuinely overlapping it.
    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeCollisionTests
    {
        private Quadtree _Quadtree;
        private TestItem _Item;

        [OneTimeSetUp]
        public void Init()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
            _Item = new TestItem("Target", 10, 10, (int)TestItem.Properties.Property1); // bounding box (9.5,9.5)-(10.5,10.5)
            _Quadtree.AddItem(_Item);
        }

        [Test]
        public void Rectangle_TouchingExactlyOnRightEdge_IsFound()
        {
            var itemsFound = new List<IMapObject2d>();
            var searchArea = new Rectangle(7.5f, 9.5f, 2f, 1f); // right edge at x=9.5, spans the full y-range of the item's box

            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(_Item));
        }

        [Test]
        public void Rectangle_JustShortOfTouching_IsNotFound()
        {
            var itemsFound = new List<IMapObject2d>();
            var searchArea = new Rectangle(7.5f, 9.5f, 1.999f, 1f); // right edge at x=9.499, just short of the item's left edge

            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Is.Empty);
        }

        [Test]
        public void Circle_DistanceExactlyEqualToRadius_IsFound()
        {
            var itemsFound = new List<IMapObject2d>();
            var searchArea = new Circle(8.5f, 10f, 1f); // closest point on item's box is (9.5,10): exactly 1 unit away

            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(_Item));
        }

        [Test]
        public void Circle_DistanceJustBeyondRadius_IsNotFound()
        {
            var itemsFound = new List<IMapObject2d>();
            var searchArea = new Circle(8.499f, 10f, 1f); // closest point is just over 1 unit away

            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Is.Empty);
        }
    }
}
