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
using SpatialTrees.Octrees;
using System.Collections.Generic;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Octree")]
    public class OctreeRemoveItemTests
    {
        private Octree _Octree;

        [SetUp]
        public void Setup()
        {
            _Octree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
        }

        [Test]
        public void RemoveItem_ExistingItem_ReturnsTrueAndRemovesFromIndexAndSearchResults()
        {
            var item = new TestVolumeItem("Removable", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            _Octree.AddItem(item);

            var result = _Octree.RemoveItem(item);

            var itemsFound = new List<IMapObject3d>();
            _Octree.GetCollidingItems(new Cube(0, 0, 0, 100, 100, 100), (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(_Octree.ObjectIndex.ContainsKey(item), Is.False);
                Assert.That(itemsFound, Does.Not.Contain(item));
            });
        }

        [Test]
        public void RemoveItem_ItemNeverAdded_ReturnsFalse()
        {
            var item = new TestVolumeItem("NeverAdded", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);

            var result = _Octree.RemoveItem(item);

            Assert.That(result, Is.False);
        }

        [Test]
        public void RemoveItem_DroppingSubtreeToMaxObjects_CollapsesTheSplitNode()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);

            var a = new TestVolumeItem("a", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            var b = new TestVolumeItem("b", 90, 10, 10, (int)TestVolumeItem.Properties.Property1);
            var c = new TestVolumeItem("c", 10, 90, 10, (int)TestVolumeItem.Properties.Property1);
            var d = new TestVolumeItem("d", 90, 90, 90, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(a);
            tree.AddItem(b);
            tree.AddItem(c); // splits the root (node was at maxObjects)
            tree.AddItem(d);

            Assert.That(tree.TopNode.IsSplit, Is.True);

            tree.RemoveItem(c); // 3 left - still over the limit
            Assert.That(tree.TopNode.IsSplit, Is.True);

            tree.RemoveItem(d); // 2 left - node collapses back to a leaf

            Assert.Multiple((System.Action)(() =>
            {
                Assert.That((bool)tree.TopNode.IsSplit, Is.False);
                Assert.That(tree.TopNode.NodeItems, Is.EquivalentTo(new[] { a, b }));
                Assert.That(tree.ObjectIndex[a], Is.SameAs((OctreeNode)tree.TopNode));
                Assert.That(tree.ObjectIndex[b], Is.SameAs((OctreeNode)tree.TopNode));
            }));
        }
    }

    [TestFixture]
    [Category("Octree")]
    public class OctreeClearTests
    {
        [Test]
        public void Clear_RemovesAllItemsFromIndexAndSearchResults()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            tree.AddItem(new TestVolumeItem("A", 10, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("B", 50, 50, 50, (int)TestVolumeItem.Properties.Property1));

            tree.Clear();

            var itemsFound = new List<IMapObject3d>();
            var anyFound = tree.GetCollidingItems(new Cube(0, 0, 0, 100, 100, 100), (int)TestVolumeItem.Properties.Property1, ref itemsFound);

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
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);
            tree.AddItem(new TestVolumeItem("a", 10, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("b", 90, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("c", 10, 90, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("d", 90, 90, 90, (int)TestVolumeItem.Properties.Property1)); // splits the root

            Assert.That(tree.TopNode.IsSplit, Is.True);

            tree.Clear();

            Assert.Multiple((System.Action)(() =>
            {
                Assert.That((bool)tree.TopNode.IsSplit, Is.False);
                Assert.That((int)tree.TopNode.GetChildObjectCount(), Is.EqualTo(0));
            }));
        }
    }

    [TestFixture]
    [Category("Octree")]
    public class OctreeResizeTests
    {
        [Test]
        public void Resize_DoublesWorldCubeAndIncrementsMaxDepth()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            tree.Resize();

            Assert.Multiple(() =>
            {
                Assert.That(tree.WorldCube.Width, Is.EqualTo(200f));
                Assert.That(tree.WorldCube.Height, Is.EqualTo(200f));
                Assert.That(tree.WorldCube.Depth, Is.EqualTo(200f));
                Assert.That(tree.MaxDepth, Is.EqualTo(6));
            });
        }

        [Test]
        public void Resize_PreservesAbilityToFindPreviouslyAddedItems()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            var item = new TestVolumeItem("Survivor", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(item);

            tree.Resize();

            var itemsFound = new List<IMapObject3d>();
            tree.GetCollidingItems(new Cube(9, 9, 9, 11, 11, 11), (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(item));
        }

        [Test]
        public void Resize_RestampsCachedDepthOfThePushedDownSubtree()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);
            tree.AddItem(new TestVolumeItem("a", 10, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("b", 90, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("c", 10, 90, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("d", 90, 90, 90, (int)TestVolumeItem.Properties.Property1)); // splits the root

            var oldRoot = tree.TopNode;
            var oldChild = tree.TopNode[(int)eOctant.UpperRightNear];
            Assert.That(oldRoot.Depth, Is.EqualTo(1));
            Assert.That(oldChild.Depth, Is.EqualTo(2));

            tree.Resize();

            Assert.Multiple((System.Action)(() =>
            {
                Assert.That((int)tree.TopNode.Depth, Is.EqualTo(1));
                Assert.That((OctreeNode)tree.TopNode[(int)eOctant.UpperLeftNear], Is.SameAs((OctreeNode)oldRoot));
                Assert.That((int)oldRoot.Depth, Is.EqualTo(2)); // was 1, dropped a level
                Assert.That((int)oldChild.Depth, Is.EqualTo(3)); // was 2, dropped a level
            }));
        }
    }
}
