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
    [Category("Octree")]
    public class OctreeSplitRoutingTests
    {
        [Test]
        public void AddItem_OffOriginWorldCube_RoutesToCorrectOctant()
        {
            var tree = new Octree(new Cube(50, 50, 50, 150, 150, 150), 5, 6); // world spans (50,50,50)-(150,150,150)

            var upperRightNear = new TestVolumeItem("URN", 125, 75, 75, (int)TestVolumeItem.Properties.Property1);
            var lowerRightNear = new TestVolumeItem("LRN", 125, 125, 75, (int)TestVolumeItem.Properties.Property1);
            var lowerLeftNear = new TestVolumeItem("LLN", 75, 125, 75, (int)TestVolumeItem.Properties.Property1);
            var upperLeftNear = new TestVolumeItem("ULN", 75, 75, 75, (int)TestVolumeItem.Properties.Property1);
            var upperRightFar = new TestVolumeItem("URF", 125, 75, 125, (int)TestVolumeItem.Properties.Property1);
            var lowerRightFar = new TestVolumeItem("LRF", 125, 125, 125, (int)TestVolumeItem.Properties.Property1);
            var lowerLeftFar = new TestVolumeItem("LLF", 75, 125, 125, (int)TestVolumeItem.Properties.Property1);
            var upperLeftFar = new TestVolumeItem("ULF", 75, 75, 125, (int)TestVolumeItem.Properties.Property1);

            tree.AddItem(upperRightNear);
            tree.AddItem(lowerRightNear);
            tree.AddItem(lowerLeftNear);
            tree.AddItem(upperLeftNear);
            tree.AddItem(upperRightFar);
            tree.AddItem(lowerRightFar);
            tree.AddItem(lowerLeftFar); // arrives with the node already at maxObjects(6), triggering Split()
            tree.AddItem(upperLeftFar);

            Assert.Multiple((System.Action)(() =>
            {
                Assert.That(tree.TopNode[(int)eOctant.UpperRightNear].NodeItems, Does.Contain(upperRightNear));
                Assert.That(tree.TopNode[(int)eOctant.LowerRightNear].NodeItems, Does.Contain(lowerRightNear));
                Assert.That(tree.TopNode[(int)eOctant.LowerLeftNear].NodeItems, Does.Contain(lowerLeftNear));
                Assert.That(tree.TopNode[(int)eOctant.UpperLeftNear].NodeItems, Does.Contain(upperLeftNear));
                Assert.That(tree.TopNode[(int)eOctant.UpperRightFar].NodeItems, Does.Contain(upperRightFar));
                Assert.That(tree.TopNode[(int)eOctant.LowerRightFar].NodeItems, Does.Contain(lowerRightFar));
                Assert.That(tree.TopNode[(int)eOctant.LowerLeftFar].NodeItems, Does.Contain(lowerLeftFar));
                Assert.That(tree.TopNode[(int)eOctant.UpperLeftFar].NodeItems, Does.Contain(upperLeftFar));
            }));
        }

        [Test]
        public void AddItem_ItemsConcentratedInOneOctant_SplitsThatOctantAgain()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 6);

            // all within the upper-right-near octant (x>50, y<50, z<50), forcing a second-level split within it
            var a = new TestVolumeItem("A", 60, 10, 10, (int)TestVolumeItem.Properties.Property1);
            var b = new TestVolumeItem("B", 90, 10, 10, (int)TestVolumeItem.Properties.Property1);
            var c = new TestVolumeItem("C", 60, 40, 10, (int)TestVolumeItem.Properties.Property1);
            var d = new TestVolumeItem("D", 90, 40, 10, (int)TestVolumeItem.Properties.Property1);
            var e = new TestVolumeItem("E", 60, 10, 40, (int)TestVolumeItem.Properties.Property1);
            var f = new TestVolumeItem("F", 90, 10, 40, (int)TestVolumeItem.Properties.Property1);
            var g = new TestVolumeItem("G", 60, 40, 40, (int)TestVolumeItem.Properties.Property1);
            var h = new TestVolumeItem("H", 90, 40, 40, (int)TestVolumeItem.Properties.Property1);

            tree.AddItem(a);
            tree.AddItem(b);
            tree.AddItem(c);
            tree.AddItem(d);
            tree.AddItem(e);
            tree.AddItem(f);
            tree.AddItem(g);
            tree.AddItem(h);

            var upperRightNearChild = tree.TopNode[(int)eOctant.UpperRightNear];

            Assert.Multiple(() =>
            {
                Assert.That(upperRightNearChild.GetChildObjectCount(), Is.EqualTo(8));
                // second-level split within that octant (sub-center at 75,25,25)
                Assert.That(upperRightNearChild[(int)eOctant.UpperRightNear].NodeItems, Does.Contain(b));
                Assert.That(upperRightNearChild[(int)eOctant.LowerRightNear].NodeItems, Does.Contain(d));
                Assert.That(upperRightNearChild[(int)eOctant.LowerLeftNear].NodeItems, Does.Contain(c));
                Assert.That(upperRightNearChild[(int)eOctant.UpperLeftNear].NodeItems, Does.Contain(a));
                Assert.That(upperRightNearChild[(int)eOctant.UpperRightFar].NodeItems, Does.Contain(f));
                Assert.That(upperRightNearChild[(int)eOctant.LowerRightFar].NodeItems, Does.Contain(h));
                Assert.That(upperRightNearChild[(int)eOctant.LowerLeftFar].NodeItems, Does.Contain(g));
                Assert.That(upperRightNearChild[(int)eOctant.UpperLeftFar].NodeItems, Does.Contain(e));
            });
        }

        [Test]
        public void GetCollidingItems_AfterMultiLevelSplit_StillFindsItemAtItsTrueLocation()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 6);

            var target = new TestVolumeItem("Target", 90, 10, 10, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(new TestVolumeItem("A", 60, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(target);
            tree.AddItem(new TestVolumeItem("C", 60, 40, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("D", 90, 40, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("E", 60, 10, 40, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("F", 90, 10, 40, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("G", 60, 40, 40, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("H", 90, 40, 40, (int)TestVolumeItem.Properties.Property1));

            var itemsFound = new HashSet<IMapObject3d>();
            tree.GetCollidingItems(new Cube(89, 9, 9, 91, 11, 11), (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(target));
        }

        [Test]
        public void AddItem_ItemStraddlingOctantBoundary_StaysOnParentNode()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);

            // one small item per near octant, plus a large item centered on the root split
            // point whose 20x20x20 box overlaps all eight children and fits inside none.
            var straddle = new TestVolumeItem("Straddle", 50, 50, 50, 20f, 20f, 20f, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(new TestVolumeItem("A", 25, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("B", 75, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("C", 25, 75, 25, (int)TestVolumeItem.Properties.Property1)); // triggers Split() (node was at maxObjects)
            tree.AddItem(straddle); // routes into the split root; no child contains it, so it stays on the root

            Assert.Multiple((System.Action)(() =>
            {
                Assert.That((OctreeNode)tree.TopNode[(int)eOctant.UpperLeftNear], Is.Not.Null); // did split
                Assert.That(tree.TopNode.NodeItems, Does.Contain(straddle));
                Assert.That(tree.ObjectIndex[straddle], Is.SameAs((OctreeNode)tree.TopNode));
            }));
        }

        [Test]
        public void GetCollidingItems_StraddlingItem_FoundFromNeighbouringOctantOnly()
        {
            var tree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 2);

            var straddle = new TestVolumeItem("Straddle", 50, 50, 50, 20f, 20f, 20f, (int)TestVolumeItem.Properties.Property1); // box (40,40,40)-(60,60,60)
            tree.AddItem(new TestVolumeItem("A", 25, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("B", 75, 25, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("C", 25, 75, 25, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(straddle);

            // search box sits entirely inside the lower-right-far octant but overlaps the
            // straddling item. Before routing accounted for extent this returned nothing.
            var itemsFound = new HashSet<IMapObject3d>();
            tree.GetCollidingItems(new Cube(55, 55, 55, 58, 58, 58), (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(straddle));
        }
    }
}
