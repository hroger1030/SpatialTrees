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
    [TestFixture]
    [Category("Quadtree")]
    public class SpatialTreesTests
    {
        private Quadtree _Quadtree;

        [OneTimeSetUp]
        public void Init()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            _Quadtree.AddItem(new TestItem() { Name = "TestItem1", Location = new Point2(1, 1), ObjectTypes = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem2", Location = new Point2(5, 5), ObjectTypes = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem3", Location = new Point2(25, 25), ObjectTypes = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem4", Location = new Point2(50, 50), ObjectTypes = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem5", Location = new Point2(75, 75), ObjectTypes = (int)TestItem.Properties.Property3 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem6", Location = new Point2(100, 100), ObjectTypes = (int)TestItem.Properties.All });
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquare_Passes()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Rectangle(1, 1, 1, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquareOversized_Passes()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Rectangle(-1, -1, 102, 102);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 3, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircle_Passes()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Circle(1, 1, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircleOversized_Passes()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Circle(50, 50, 100);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 3, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsWithMatchingProperty_Passes()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Circle(3, 3, 5);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property2, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        // Renamed from "...SquareTangent_Passes": the search rectangle (0,0)-(1,1) and TestItem1's
        // bounding box (0.5,0.5)-(1.5,1.5) genuinely overlap over a 0.5x0.5 area, they don't just touch
        // at an edge/corner. See QuadtreeGetCollidingItemsTangencyTests for true zero-area tangency cases.
        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquareOverlapping_Passes()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Rectangle(0, 0, 1, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        // Renamed from "...CircleTangent_Passes": the circle center (1,2)/radius 1 comes within 0.5 units
        // of TestItem1's bounding box, well inside the radius rather than exactly touching it.
        // See QuadtreeGetCollidingItemsTangencyTests for true boundary-distance tangency.
        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircleOverlapping_Passes()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Circle(1, 2, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsRectangle_NoIntersection_ReturnsFalseAndEmptySet()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Rectangle(40, 0, 5, 5); // clear of every seeded item
            var result = _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.All, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(itemsFound, Is.Empty);
            });
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsCircle_NoIntersection_ReturnsFalseAndEmptySet()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Circle(40, 0, 2); // clear of every seeded item
            var result = _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.All, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(itemsFound, Is.Empty);
            });
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItems_ReturnValueTrueWhenAnyItemFound()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Rectangle(1, 1, 1, 1);
            var result = _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(result, Is.True);
        }

        // TestItem6 carries Properties.All, so any single-flag search mask is a subset of its flags
        // and should match it regardless of how the query happens to prune the tree (a small area that
        // only touches TestItem6's own bounding box, vs one large enough to fully contain the node).
        [Test]
        [Category("Quadtree")]
        public void Quadtree_ObjectTypeMask_PartialOverlapSearch_IncludesItemWithAllFlagsSet()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Rectangle(99, 99, 2, 2); // overlaps TestItem6 only, far smaller than the world rectangle
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Has.Some.Property(nameof(TestItem.Name)).EqualTo("TestItem6"));
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_ObjectTypeMask_SearchAreaContainsWholeNode_IncludesItemWithAllFlagsSet()
        {
            var itemsFound = new HashSet<IMapObject2d>();
            var searchArea = new Rectangle(-1, -1, 102, 102); // contains the entire world rectangle
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Has.Some.Property(nameof(TestItem.Name)).EqualTo("TestItem6"));
        }
    }
}
