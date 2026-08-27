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
    public class OctreeAddItemTests
    {
        private Octree _Octree;

        [SetUp]
        public void Setup()
        {
            _Octree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
        }

        [Test]
        public void AddItem_ValidItem_IsIndexed()
        {
            var item = new TestVolumeItem("A", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);

            _Octree.AddItem(item);

            Assert.That(_Octree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void AddItem_LocationOutsideWorldCube_ThrowsArgumentException()
        {
            var item = new TestVolumeItem("Outside", 500, 500, 500, (int)TestVolumeItem.Properties.Property1);

            Assert.Throws<ArgumentException>(() => _Octree.AddItem(item));
        }

        [Test]
        public void AddItem_ZeroObjectTypes_Throws()
        {
            var item = new TestVolumeItem("NoType", 10, 10, 10, 0);

            Assert.Throws<Exception>(() => _Octree.AddItem(item));
        }

        [Test]
        public void AddItem_SameReferenceAddedTwiceAtNewLocation_TreatedAsUpdate()
        {
            var item = new TestVolumeItem("Moving", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            _Octree.AddItem(item);

            item.Location = new Point3(90, 90, 90);
            _Octree.AddItem(item);

            Assert.That(_Octree.ObjectIndex.Count, Is.EqualTo(1));

            var itemsFound = new HashSet<IMapObject3d>();
            _Octree.GetCollidingItems(new Cube(0, 0, 0, 100, 100, 100), (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Has.Count.EqualTo(1));
        }

        [Test]
        public void AddItem_ExceedingMaxObjects_SplitsRootIntoOctantsWithCorrectMembership()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 6);

            var upperRightNear = new TestVolumeItem("URN", 75, 25, 25, (int)TestVolumeItem.Properties.Property1);
            var lowerRightNear = new TestVolumeItem("LRN", 75, 75, 25, (int)TestVolumeItem.Properties.Property1);
            var lowerLeftNear = new TestVolumeItem("LLN", 25, 75, 25, (int)TestVolumeItem.Properties.Property1);
            var upperLeftNear = new TestVolumeItem("ULN", 25, 25, 25, (int)TestVolumeItem.Properties.Property1);
            var upperRightFar = new TestVolumeItem("URF", 75, 25, 75, (int)TestVolumeItem.Properties.Property1);
            var lowerRightFar = new TestVolumeItem("LRF", 75, 75, 75, (int)TestVolumeItem.Properties.Property1);
            var lowerLeftFar = new TestVolumeItem("LLF", 25, 75, 75, (int)TestVolumeItem.Properties.Property1);
            var upperLeftFar = new TestVolumeItem("ULF", 25, 25, 75, (int)TestVolumeItem.Properties.Property1);

            tree.AddItem(upperRightNear);
            tree.AddItem(lowerRightNear);
            tree.AddItem(lowerLeftNear);
            tree.AddItem(upperLeftNear);
            tree.AddItem(upperRightFar);
            tree.AddItem(lowerRightFar);
            tree.AddItem(lowerLeftFar); // arrives with the node already at maxObjects(6), triggering Split()
            tree.AddItem(upperLeftFar);

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode[(int)eOctant.UpperRightNear].NodeItems, Does.Contain(upperRightNear));
                Assert.That(tree.TopNode[(int)eOctant.LowerRightNear].NodeItems, Does.Contain(lowerRightNear));
                Assert.That(tree.TopNode[(int)eOctant.LowerLeftNear].NodeItems, Does.Contain(lowerLeftNear));
                Assert.That(tree.TopNode[(int)eOctant.UpperLeftNear].NodeItems, Does.Contain(upperLeftNear));
                Assert.That(tree.TopNode[(int)eOctant.UpperRightFar].NodeItems, Does.Contain(upperRightFar));
                Assert.That(tree.TopNode[(int)eOctant.LowerRightFar].NodeItems, Does.Contain(lowerRightFar));
                Assert.That(tree.TopNode[(int)eOctant.LowerLeftFar].NodeItems, Does.Contain(lowerLeftFar));
                Assert.That(tree.TopNode[(int)eOctant.UpperLeftFar].NodeItems, Does.Contain(upperLeftFar));
                Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(8));
            });
        }

        // The tree routes and range-checks by BoundingBox.Center, not Location. These two
        // cases pin that down with an item whose Location and BoundingBox deliberately disagree.
        [Test]
        public void AddItem_LocationOutsideWorldButBoundingBoxCenterInside_Succeeds()
        {
            var item = new DivergentVolumeItem
            {
                ObjectTypes = (int)TestVolumeItem.Properties.Property1,
                Location = new Point3(500, 500, 500),                  // outside the world cube
                BoundingBox = new Cube(49, 49, 49, 51, 51, 51),        // center well inside it
            };

            _Octree.AddItem(item);

            Assert.That(_Octree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void AddItem_LocationInsideWorldButBoundingBoxCenterOutside_ThrowsArgumentException()
        {
            var item = new DivergentVolumeItem
            {
                ObjectTypes = (int)TestVolumeItem.Properties.Property1,
                Location = new Point3(50, 50, 50),                     // inside the world cube
                BoundingBox = new Cube(499, 499, 499, 501, 501, 501),  // center outside it
            };

            Assert.Throws<ArgumentException>(() => _Octree.AddItem(item));
        }

        // Re-adding a known item is treated as an update: it must end up indexed against
        // the node that actually holds it, with no leftover entry on its previous node.
        [Test]
        public void AddItem_ReAddKnownItemAtNewLocation_ObjectIndexPointsAtHoldingNode()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);

            tree.AddItem(new TestVolumeItem("a", 10, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("b", 90, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("c", 10, 90, 10, (int)TestVolumeItem.Properties.Property1)); // splits the root (node was at maxObjects)
            var mover = new TestVolumeItem("mover", 90, 90, 90, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(mover);

            var firstNode = tree.ObjectIndex[mover];

            mover.Location = new Point3(12, 12, 12);
            tree.AddItem(mover);

            var secondNode = tree.ObjectIndex[mover];

            Assert.Multiple(() =>
            {
                Assert.That(tree.ObjectIndex, Has.Count.EqualTo(4));
                Assert.That(secondNode, Is.Not.SameAs(firstNode));
                Assert.That(secondNode.NodeItems, Does.Contain(mover));
                Assert.That(firstNode.NodeItems, Does.Not.Contain(mover));
            });
        }

        // A map object whose Location is not tied to its BoundingBox, for tests that need
        // the two to diverge.
        public class DivergentVolumeItem : IMapObject3d
        {
            public int ObjectTypes { get; set; }
            public Point3 Location { get; set; }
            public Cube BoundingBox { get; set; }
        }
    }
}
