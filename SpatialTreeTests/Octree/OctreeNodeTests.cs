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
using System;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("OctreeNode")]
    public class OctreeNodeTests
    {
        [Test]
        public void Indexer_IndexBelowZero_ThrowsIndexOutOfRangeException()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            Assert.Throws<IndexOutOfRangeException>((Action)(() => { var _ = tree.TopNode[-1]; }));
        }

        [Test]
        public void Indexer_IndexAtLeafCount_ThrowsIndexOutOfRangeException()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            Assert.Throws<IndexOutOfRangeException>((Action)(() => { var _ = tree.TopNode[OctreeNode.LEAVES]; }));
        }

        [Test]
        public void Indexer_ValidIndexBeforeAnySplit_ThrowsNullReferenceException()
        {
            // documents current behavior: a node's leaves array is only allocated by Split(),
            // so indexing an in-range octant on an unsplit node throws instead of returning null.
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            Assert.Throws<NullReferenceException>((Action)(() => { var _ = tree.TopNode[(int)eOctant.UpperRightNear]; }));
        }

        [Test]
        public void Split_CalledTwiceOnSameNode_Throws()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            tree.TopNode.Split();

            Assert.Throws<Exception>(() => tree.TopNode.Split());
        }

        // A leaf holds exactly MaxNodeObjects items; the next one triggers the split.
        [Test]
        public void AddItem_LeafFillsToMaxNodeObjectsThenSplitsOnTheNext()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 3);

            tree.AddItem(new TestVolumeItem("1", 25, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("2", 75, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("3", 25, 75, 25, (int)TestVolumeItem.Properties.Property1));

            Assert.Multiple((Action)(() =>
            {
                Assert.That((bool)tree.TopNode.IsSplit, Is.False);
                Assert.That(tree.TopNode.NodeItems, Has.Count.EqualTo(3)); // exactly MaxNodeObjects, still a leaf
            }));

            tree.AddItem(new TestVolumeItem("4", 75, 75, 75, (int)TestVolumeItem.Properties.Property1));

            Assert.That(tree.TopNode.IsSplit, Is.True);
        }

        [Test]
        public void Depth_RootNodeIsOne()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            Assert.That(tree.TopNode.Depth, Is.EqualTo(1));
        }

        [Test]
        public void Depth_ChildOfRootIsTwo()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);

            // two items in the upper-right-near octant plus one elsewhere fills the root
            // and splits it; that child is materialised as those two route into it.
            tree.AddItem(new TestVolumeItem("a", 60, 40, 40, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("b", 90, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("c", 10, 90, 90, (int)TestVolumeItem.Properties.Property1));

            Assert.That(tree.TopNode[(int)eOctant.UpperRightNear].Depth, Is.EqualTo(2));
        }

        [Test]
        public void GetChildObjectCount_CountsItemsAcrossAllLeaves()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 6);

            tree.AddItem(new TestVolumeItem("URN", 75, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("LRN", 75, 75, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("LLN", 25, 75, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("ULN", 25, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("URF", 75, 25, 75, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("LRF", 75, 75, 75, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("LLF", 25, 75, 75, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("ULF", 25, 25, 75, (int)TestVolumeItem.Properties.Property1));

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(8));
        }

        [Test]
        public void AddItem_AtMaxDepth_NeverSplitsEvenWhenExceedingMaxObjects()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 1, 1);

            tree.AddItem(new TestVolumeItem("A", 10, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("B", 11, 11, 11, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("C", 12, 12, 12, (int)TestVolumeItem.Properties.Property1));

            Assert.Multiple((Action)(() =>
            {
                Assert.That((int)tree.TopNode.GetChildObjectCount(), Is.EqualTo(3));
                Assert.Throws<NullReferenceException>((Action)(() => { var _ = tree.TopNode[(int)eOctant.UpperRightNear]; }));
            }));
        }

        // Guards against re-introducing a broken value-equality override: nodes and
        // trees use reference identity, so different instances are never "equal".
        [Test]
        public void Equality_UsesReferenceIdentity_NotBoundingBox()
        {
            var a = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            var b = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            b.AddItem(new TestVolumeItem("only-in-b", 10, 10, 10, (int)TestVolumeItem.Properties.Property1));

            Assert.Multiple((Action)(() =>
            {
                Assert.That(a.Equals(b), Is.False);          // same world, different contents
                Assert.That(a.Equals(a), Is.True);
                Assert.That(a.TopNode.Equals((object)b.TopNode), Is.False);
                Assert.That(a.TopNode.Equals((object)a.TopNode), Is.True);
            }));
        }
    }
}
