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
    public class QuadtreeSplitRoutingTests
    {
        [Test]
        public void AddItem_OffOriginWorldRectangle_RoutesToCorrectQuadrant()
        {
            var tree = new Quadtree(new Rectangle(50, 50, 100, 100), 5, 2); // world spans (50,50)-(150,150)

            var upperRight = new TestItem("UR", 125, 75, (int)TestItem.Properties.Property1);
            var lowerRight = new TestItem("LR", 125, 125, (int)TestItem.Properties.Property1);
            var lowerLeft = new TestItem("LL", 75, 125, (int)TestItem.Properties.Property1);
            var upperLeft = new TestItem("UL", 75, 75, (int)TestItem.Properties.Property1);

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
            });
        }

        [Test]
        public void AddItem_ItemsConcentratedInOneQuadrant_SplitsThatQuadrantAgain()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            // all in the upper-right quadrant (x>50, y<50), forcing a second-level split within it
            var a = new TestItem("A", 60, 10, (int)TestItem.Properties.Property1);
            var b = new TestItem("B", 90, 10, (int)TestItem.Properties.Property1);
            var c = new TestItem("C", 60, 40, (int)TestItem.Properties.Property1);
            var d = new TestItem("D", 90, 40, (int)TestItem.Properties.Property1);

            tree.AddItem(a);
            tree.AddItem(b);
            tree.AddItem(c);
            tree.AddItem(d);

            var upperRightChild = tree.TopNode[(int)eQuadrant.UpperRightQuadrant];

            Assert.Multiple(() =>
            {
                Assert.That(upperRightChild.GetChildObjectCount(), Is.EqualTo(4));
                // second-level split: A/C (x=60, left half) and B/D (x=90, right half) must separate
                Assert.That(upperRightChild[(int)eQuadrant.UpperRightQuadrant].NodeItems, Does.Contain(b));
                Assert.That(upperRightChild[(int)eQuadrant.LowerRightQuadrant].NodeItems, Does.Contain(d));
                Assert.That(upperRightChild[(int)eQuadrant.LowerLeftQuadrant].NodeItems, Does.Contain(c));
                Assert.That(upperRightChild[(int)eQuadrant.UpperLeftQuadrant].NodeItems, Does.Contain(a));
            });
        }

        [Test]
        public void GetCollidingItems_AfterMultiLevelSplit_StillFindsItemAtItsTrueLocation()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            var target = new TestItem("Target", 90, 10, (int)TestItem.Properties.Property1);
            tree.AddItem(new TestItem("A", 60, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(target);
            tree.AddItem(new TestItem("C", 60, 40, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("D", 90, 40, (int)TestItem.Properties.Property1));

            var itemsFound = new HashSet<IMapObject2d>();
            tree.GetCollidingItems(new Rectangle(89, 9, 2, 2), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(target));
        }

        [Test]
        public void AddItem_ItemStraddlingQuadrantBoundary_StaysOnParentNode()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            // one small item per quadrant, plus a large item centered on the root split
            // point whose 20x20 box overlaps all four children and fits inside none.
            var straddle = new TestItem("Straddle", 50, 50, 20f, 20f, (int)TestItem.Properties.Property1);
            tree.AddItem(new TestItem("A", 25, 25, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("B", 75, 25, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("C", 25, 75, (int)TestItem.Properties.Property1));
            tree.AddItem(straddle); // 4th item triggers Split()

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode[(int)eQuadrant.UpperLeftQuadrant], Is.Not.Null); // did split
                Assert.That(tree.TopNode.NodeItems, Does.Contain(straddle));
                Assert.That(tree.ObjectIndex[straddle], Is.SameAs(tree.TopNode));
            });
        }

        [Test]
        public void GetCollidingItems_StraddlingItem_FoundFromNeighbouringQuadrantOnly()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            var straddle = new TestItem("Straddle", 50, 50, 20f, 20f, (int)TestItem.Properties.Property1); // box (40,40)-(60,60)
            tree.AddItem(new TestItem("A", 25, 25, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("B", 75, 25, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("C", 25, 75, (int)TestItem.Properties.Property1));
            tree.AddItem(straddle);

            // search box sits entirely inside the lower-right quadrant but overlaps the
            // straddling item. Before routing accounted for extent this returned nothing.
            var itemsFound = new HashSet<IMapObject2d>();
            tree.GetCollidingItems(new Rectangle(55, 55, 3, 3), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(straddle));
        }
    }
}
