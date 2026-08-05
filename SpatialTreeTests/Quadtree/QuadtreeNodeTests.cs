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
    [Category("QuadtreeNode")]
    public class QuadtreeNodeTests
    {
        [Test]
        public void Indexer_IndexBelowZero_ThrowsIndexOutOfRangeException()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            Assert.Throws<IndexOutOfRangeException>(() => { var _ = tree.TopNode[-1]; });
        }

        [Test]
        public void Indexer_IndexAtLeafCount_ThrowsIndexOutOfRangeException()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            Assert.Throws<IndexOutOfRangeException>(() => { var _ = tree.TopNode[QuadtreeNode.LEAVES]; });
        }

        [Test]
        public void Indexer_ValidIndexBeforeAnySplit_ThrowsNullReferenceException()
        {
            // documents current behavior: a node's leaves array is only allocated by Split(),
            // so indexing an in-range quadrant on an unsplit node throws instead of returning null.
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            Assert.Throws<NullReferenceException>(() => { var _ = tree.TopNode[(int)eQuadrant.UpperRightQuadrant]; });
        }

        [Test]
        public void Split_CalledTwiceOnSameNode_Throws()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
            tree.TopNode.Split();

            Assert.Throws<Exception>(() => tree.TopNode.Split());
        }

        [Test]
        public void Depth_RootNodeIsOne()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            Assert.That(tree.TopNode.Depth, Is.EqualTo(1));
        }

        [Test]
        public void Depth_ChildOfRootIsTwo()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
            tree.TopNode.Split();

            Assert.That(tree.TopNode[(int)eQuadrant.UpperRightQuadrant].Depth, Is.EqualTo(2));
        }

        [Test]
        public void GetChildObjectCount_CountsItemsAcrossAllLeaves()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 2);

            tree.AddItem(new TestItem("UR", 75, 25, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("LR", 75, 75, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("LL", 25, 75, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("UL", 25, 25, (int)TestItem.Properties.Property1));

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(4));
        }

        [Test]
        public void AddItem_AtMaxDepth_NeverSplitsEvenWhenExceedingMaxObjects()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 1, 1);

            tree.AddItem(new TestItem("A", 10, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("B", 11, 11, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("C", 12, 12, (int)TestItem.Properties.Property1));

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(3));
                Assert.Throws<NullReferenceException>(() => { var _ = tree.TopNode[(int)eQuadrant.UpperRightQuadrant]; });
            });
        }
    }
}
