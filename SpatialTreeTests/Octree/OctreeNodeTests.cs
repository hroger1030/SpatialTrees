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

            Assert.Throws<IndexOutOfRangeException>(() => { var _ = tree.TopNode[-1]; });
        }

        [Test]
        public void Indexer_IndexAtLeafCount_ThrowsIndexOutOfRangeException()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            Assert.Throws<IndexOutOfRangeException>(() => { var _ = tree.TopNode[OctreeNode.LEAVES]; });
        }

        [Test]
        public void Indexer_ValidIndexBeforeAnySplit_ThrowsNullReferenceException()
        {
            // documents current behavior: a node's leaves array is only allocated by Split(),
            // so indexing an in-range octant on an unsplit node throws instead of returning null.
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            Assert.Throws<NullReferenceException>(() => { var _ = tree.TopNode[(int)eOctant.UpperRightNear]; });
        }

        [Test]
        public void Split_CalledTwiceOnSameNode_Throws()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            tree.TopNode.Split();

            Assert.Throws<Exception>(() => tree.TopNode.Split());
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
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            tree.TopNode.Split();

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

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(3));
                Assert.Throws<NullReferenceException>(() => { var _ = tree.TopNode[(int)eOctant.UpperRightNear]; });
            });
        }
    }
}
