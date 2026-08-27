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
using System;
using System.Collections.Generic;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeAddItemTests
    {
        private Quadtree _Quadtree;

        [SetUp]
        public void Setup()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
        }

        [Test]
        public void AddItem_ValidItem_ReturnsTrueAndIsIndexed()
        {
            var item = new TestItem("A", 10, 10, (int)TestItem.Properties.Property1);

            var result = _Quadtree.AddItem(item);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(_Quadtree.ObjectIndex.ContainsKey(item), Is.True);
            });
        }

        [Test]
        public void AddItem_LocationOutsideWorldRectangle_ThrowsArgumentException()
        {
            var item = new TestItem("Outside", 500, 500, (int)TestItem.Properties.Property1);

            Assert.Throws<ArgumentException>(() => _Quadtree.AddItem(item));
        }

        [Test]
        public void AddItem_ZeroObjectTypes_Throws()
        {
            var item = new TestItem("NoType", 10, 10, 0);

            Assert.Throws<Exception>(() => _Quadtree.AddItem(item));
        }

        [Test]
        public void AddItem_SameReferenceAddedTwiceAtNewLocation_TreatedAsUpdate()
        {
            var item = new TestItem("Moving", 10, 10, (int)TestItem.Properties.Property1);
            _Quadtree.AddItem(item);

            item.Location = new Point2(90, 90);
            _Quadtree.AddItem(item);

            Assert.That(_Quadtree.ObjectIndex.Count, Is.EqualTo(1));

            var itemsFound = new HashSet<IMapObject2d>();
            _Quadtree.GetCollidingItems(new Rectangle(0, 0, 100, 100), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Has.Count.EqualTo(1));
        }

        [Test]
        public void AddItem_ExceedingMaxObjects_SplitsRootIntoQuadrantsWithCorrectMembership()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            var upperRight = new TestItem("UR", 75, 25, (int)TestItem.Properties.Property1);
            var lowerRight = new TestItem("LR", 75, 75, (int)TestItem.Properties.Property1);
            var lowerLeft = new TestItem("LL", 25, 75, (int)TestItem.Properties.Property1);
            var upperLeft = new TestItem("UL", 25, 25, (int)TestItem.Properties.Property1);

            tree.AddItem(upperRight);
            tree.AddItem(lowerRight);
            tree.AddItem(lowerLeft);
            tree.AddItem(upperLeft); // 4th item pushes count above maxObjects(2), triggering Split()

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode[(int)eQuadrant.UpperRightQuadrant].NodeItems, Does.Contain(upperRight));
                Assert.That(tree.TopNode[(int)eQuadrant.LowerRightQuadrant].NodeItems, Does.Contain(lowerRight));
                Assert.That(tree.TopNode[(int)eQuadrant.LowerLeftQuadrant].NodeItems, Does.Contain(lowerLeft));
                Assert.That(tree.TopNode[(int)eQuadrant.UpperLeftQuadrant].NodeItems, Does.Contain(upperLeft));
                Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(4));
            });
        }

        // The tree routes and range-checks by BoundingBox.Center, not Location. These two
        // cases pin that down with an item whose Location and BoundingBox deliberately disagree.
        [Test]
        public void AddItem_LocationOutsideWorldButBoundingBoxCenterInside_Succeeds()
        {
            var item = new DivergentItem
            {
                ObjectTypes = (int)TestItem.Properties.Property1,
                Location = new Point2(500, 500),                          // outside the world rectangle
                BoundingBox = new Rectangle(new Point2(50, 50), 2f, 2f),  // center well inside it
            };

            Assert.Multiple(() =>
            {
                Assert.That(_Quadtree.AddItem(item), Is.True);
                Assert.That(_Quadtree.ObjectIndex.ContainsKey(item), Is.True);
            });
        }

        [Test]
        public void AddItem_LocationInsideWorldButBoundingBoxCenterOutside_ThrowsArgumentException()
        {
            var item = new DivergentItem
            {
                ObjectTypes = (int)TestItem.Properties.Property1,
                Location = new Point2(50, 50),                              // inside the world rectangle
                BoundingBox = new Rectangle(new Point2(500, 500), 2f, 2f),  // center outside it
            };

            Assert.Throws<ArgumentException>(() => _Quadtree.AddItem(item));
        }

        // A map object whose Location is not tied to its BoundingBox, for tests that need
        // the two to diverge.
        public class DivergentItem : IMapObject2d
        {
            public int ObjectTypes { get; set; }
            public Point2 Location { get; set; }
            public Rectangle BoundingBox { get; set; }
        }
    }
}
