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
    [Category("Octree")]
    public class OctreeMoveItemTests
    {
        private Octree _Octree;

        [SetUp]
        public void Setup()
        {
            _Octree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
        }

        [Test]
        public void MoveItem_NotPreviouslyTracked_AddsItAsNew()
        {
            var item = new TestVolumeItem("Fresh", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);

            _Octree.MoveItem(item);

            Assert.That(_Octree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void MoveItem_WithinSameNode_IsFindableAtNewLocation()
        {
            var item = new TestVolumeItem("Mover", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            _Octree.AddItem(item);

            item.Location = new Point3(20, 20, 20);
            _Octree.MoveItem(item);

            var itemsFound = new List<IMapObject3d>();
            _Octree.GetCollidingItems(new Cube(19, 19, 19, 21, 21, 21), (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(item));
        }

        [Test]
        public void MoveItem_ToLocationOutsideWorldCube_ThrowsArgumentException()
        {
            var item = new TestVolumeItem("Wanderer", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            _Octree.AddItem(item);

            item.Location = new Point3(500, 500, 500);

            Assert.Throws<ArgumentException>(() => _Octree.MoveItem(item));
        }

        [Test]
        public void MoveItem_ItemShrinksToFitAChildOfItsCurrentNode_IsPushedDown()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);

            // force the root to split, then add a large item that straddles the root's
            // octant boundary so it is stored on the (now split) root itself.
            tree.AddItem(new TestVolumeItem("A", 25, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("B", 75, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("C", 25, 75, 25, (int)TestVolumeItem.Properties.Property1)); // triggers Split()

            var item = new TestVolumeItem("Shrinker", 60, 60, 60, 40f, 40f, 40f, (int)TestVolumeItem.Properties.Property1); // box (40,40,40)-(80,80,80)
            tree.AddItem(item);
            Assume.That(tree.ObjectIndex[item], Is.SameAs(tree.TopNode)); // straddles - lives on the root

            // shrink it so its box now fits entirely inside the lower-right-far octant
            item.Width = 10f;
            item.Height = 10f;
            item.Depth = 10f; // box (55,55,55)-(65,65,65)
            tree.MoveItem(item);

            var lowerRightFar = tree.TopNode[(int)eOctant.LowerRightFar];

            Assert.Multiple((Action)(() =>
            {
                Assert.That(tree.TopNode.NodeItems, Does.Not.Contain(item));
                Assert.That(lowerRightFar.NodeItems, Does.Contain(item));
                Assert.That(tree.ObjectIndex[item], Is.SameAs((OctreeNode)lowerRightFar));
            }));
        }

        [Test]
        public void MoveItem_ToLocationOutsideWorldCube_LeavesItemTracked()
        {
            var item = new TestVolumeItem("Wanderer", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            _Octree.AddItem(item);
            var original_node = _Octree.ObjectIndex[item];

            item.Location = new Point3(500, 500, 500);

            Assert.Throws<ArgumentException>(() => _Octree.MoveItem(item));

            // the rejected move must not have dropped the item from the tree
            Assert.Multiple(() =>
            {
                Assert.That(_Octree.ObjectIndex.ContainsKey(item), Is.True);
                Assert.That(_Octree.ObjectIndex[item], Is.SameAs(original_node));
                Assert.That(original_node.NodeItems, Does.Contain(item));
            });
        }
    }
}
