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
    public class QuadtreeRemoveItemTests
    {
        private Quadtree _Quadtree;

        [SetUp]
        public void Setup()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
        }

        [Test]
        public void RemoveItem_ExistingItem_ReturnsTrueAndRemovesFromIndexAndSearchResults()
        {
            var item = new TestItem("Removable", 10, 10, (int)TestItem.Properties.Property1);
            _Quadtree.AddItem(item);

            var result = _Quadtree.RemoveItem(item);

            var itemsFound = new HashSet<IMapObject2d>();
            _Quadtree.GetCollidingItems(new Rectangle(0, 0, 100, 100), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(_Quadtree.ObjectIndex.ContainsKey(item), Is.False);
                Assert.That(itemsFound, Does.Not.Contain(item));
            });
        }

        [Test]
        public void RemoveItem_ItemNeverAdded_ReturnsFalse()
        {
            var item = new TestItem("NeverAdded", 10, 10, (int)TestItem.Properties.Property1);

            var result = _Quadtree.RemoveItem(item);

            Assert.That(result, Is.False);
        }

        [Test]
        public void RemoveItem_DroppingSubtreeToMaxObjects_CollapsesTheSplitNode()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            var a = new TestItem("a", 10, 10, (int)TestItem.Properties.Property1);
            var b = new TestItem("b", 90, 10, (int)TestItem.Properties.Property1);
            var c = new TestItem("c", 10, 90, (int)TestItem.Properties.Property1);
            var d = new TestItem("d", 90, 90, (int)TestItem.Properties.Property1);
            tree.AddItem(a);
            tree.AddItem(b);
            tree.AddItem(c); // splits the root (node was at maxObjects)
            tree.AddItem(d);

            Assert.That(tree.TopNode.IsSplit, Is.True);

            tree.RemoveItem(c); // 3 left - still over the limit
            Assert.That(tree.TopNode.IsSplit, Is.True);

            tree.RemoveItem(d); // 2 left - node collapses back to a leaf

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode.IsSplit, Is.False);
                Assert.That(tree.TopNode.NodeItems, Is.EquivalentTo(new[] { a, b }));
                Assert.That(tree.ObjectIndex[a], Is.SameAs(tree.TopNode));
                Assert.That(tree.ObjectIndex[b], Is.SameAs(tree.TopNode));
            });
        }

        [Test]
        public void MoveItem_EmptyingADeepSubtree_CollapsesItButLeavesTheRootSplit()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            // e/f/g keep the root split; p/q/r/s force the upper-left child to split too
            tree.AddItem(new TestItem("e", 90, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("f", 90, 90, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("g", 10, 90, (int)TestItem.Properties.Property1));
            var p = new TestItem("p", 5, 5, (int)TestItem.Properties.Property1);
            var q = new TestItem("q", 45, 5, (int)TestItem.Properties.Property1);
            var r = new TestItem("r", 5, 45, (int)TestItem.Properties.Property1);
            var s = new TestItem("s", 45, 45, (int)TestItem.Properties.Property1);
            tree.AddItem(p);
            tree.AddItem(q);
            tree.AddItem(r);
            tree.AddItem(s);

            var upperLeft = tree.TopNode[(int)eQuadrant.UpperLeftQuadrant];
            Assert.That(upperLeft.IsSplit, Is.True);

            r.Location = new Point2(95, 45);
            tree.MoveItem(r); // upper-left child down to 3
            s.Location = new Point2(95, 48);
            tree.MoveItem(s); // upper-left child down to 2 - it collapses

            var itemsFound = new HashSet<IMapObject2d>();
            tree.GetCollidingItems(new Rectangle(0, 0, 100, 100), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(upperLeft.IsSplit, Is.False);
                Assert.That(upperLeft.NodeItems, Is.EquivalentTo(new[] { p, q }));
                Assert.That(tree.TopNode.IsSplit, Is.True);
                Assert.That(itemsFound, Has.Count.EqualTo(7)); // nothing lost
            });
        }
    }

    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeClearTests
    {
        [Test]
        public void Clear_RemovesAllItemsFromIndexAndSearchResults()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
            tree.AddItem(new TestItem("A", 10, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("B", 50, 50, (int)TestItem.Properties.Property1));

            tree.Clear();

            var itemsFound = new HashSet<IMapObject2d>();
            var anyFound = tree.GetCollidingItems(new Rectangle(0, 0, 100, 100), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(tree.ObjectIndex, Is.Empty);
                Assert.That(anyFound, Is.False);
                Assert.That(itemsFound, Is.Empty);
            });
        }

        [Test]
        public void Clear_CollapsesTheSubdivisionBackToASingleLeaf()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);
            tree.AddItem(new TestItem("a", 10, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("b", 90, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("c", 10, 90, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("d", 90, 90, (int)TestItem.Properties.Property1)); // splits the root

            Assert.That(tree.TopNode.IsSplit, Is.True);

            tree.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode.IsSplit, Is.False);
                Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(0));
            });
        }
    }

    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeResizeTests
    {
        [Test]
        public void Resize_DoublesWorldRectangleAndIncrementsMaxDepth()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            var result = tree.Resize();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(tree.WorldRectangle.Width, Is.EqualTo(200f));
                Assert.That(tree.WorldRectangle.Height, Is.EqualTo(200f));
                Assert.That(tree.MaxDepth, Is.EqualTo(6));
            });
        }

        [Test]
        public void Resize_PreservesAbilityToFindPreviouslyAddedItems()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
            var item = new TestItem("Survivor", 10, 10, (int)TestItem.Properties.Property1);
            tree.AddItem(item);

            tree.Resize();

            var itemsFound = new HashSet<IMapObject2d>();
            tree.GetCollidingItems(new Rectangle(9, 9, 2, 2), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(item));
        }

        [Test]
        public void Resize_RestampsCachedDepthOfThePushedDownSubtree()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);
            tree.AddItem(new TestItem("a", 10, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("b", 90, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("c", 10, 90, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("d", 90, 90, (int)TestItem.Properties.Property1)); // splits the root

            var oldRoot = tree.TopNode;
            var oldChild = tree.TopNode[(int)eQuadrant.UpperRightQuadrant];
            Assert.That(oldRoot.Depth, Is.EqualTo(1));
            Assert.That(oldChild.Depth, Is.EqualTo(2));

            tree.Resize();

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode.Depth, Is.EqualTo(1));
                Assert.That(tree.TopNode[(int)eQuadrant.UpperLeftQuadrant], Is.SameAs(oldRoot));
                Assert.That(oldRoot.Depth, Is.EqualTo(2)); // was 1, dropped a level
                Assert.That(oldChild.Depth, Is.EqualTo(3)); // was 2, dropped a level
            });
        }
    }
}
