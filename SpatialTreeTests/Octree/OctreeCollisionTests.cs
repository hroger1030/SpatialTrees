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
    public class OctreeCollisionTests
    {
        private Octree _Octree;

        [OneTimeSetUp]
        public void Init()
        {
            _Octree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);

            _Octree.AddItem(new TestVolumeItem() { Name = "TestItem1", Location = new Point3(1, 1, 1), ObjectTypes = (int)TestVolumeItem.Properties.Property1 });
            _Octree.AddItem(new TestVolumeItem() { Name = "TestItem2", Location = new Point3(5, 5, 5), ObjectTypes = (int)TestVolumeItem.Properties.Property2 });
            _Octree.AddItem(new TestVolumeItem() { Name = "TestItem3", Location = new Point3(25, 25, 25), ObjectTypes = (int)TestVolumeItem.Properties.Property2 });
            _Octree.AddItem(new TestVolumeItem() { Name = "TestItem4", Location = new Point3(50, 50, 50), ObjectTypes = (int)TestVolumeItem.Properties.Property1 });
            _Octree.AddItem(new TestVolumeItem() { Name = "TestItem5", Location = new Point3(75, 75, 75), ObjectTypes = (int)TestVolumeItem.Properties.Property3 });
            _Octree.AddItem(new TestVolumeItem() { Name = "TestItem6", Location = new Point3(100, 100, 100), ObjectTypes = (int)TestVolumeItem.Properties.All });
        }

        [Test]
        public void Octree_FindItemsBasicCube_Passes()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(1, 1, 1, 2, 2, 2);
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        public void Octree_FindItemsBasicCubeOversized_Passes()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(-1, -1, -1, 101, 101, 101);
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 3, Is.True);
        }

        [Test]
        public void Octree_FindItemsBasicSphere_Passes()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Sphere(new Point3(1, 1, 1), 1);
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        public void Octree_FindItemsBasicSphereOversized_Passes()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Sphere(new Point3(50, 50, 50), 100);
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 3, Is.True);
        }

        [Test]
        public void Octree_FindItemsWithMatchingProperty_Passes()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Sphere(new Point3(3, 3, 3), 5);
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property2, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        public void Octree_FindItemsBasicCubeOverlapping_Passes()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(0, 0, 0, 1, 1, 1);
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        public void Octree_FindItemsBasicSphereOverlapping_Passes()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Sphere(new Point3(1, 2, 1), 1);
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        public void Octree_FindItemsCube_NoIntersection_ReturnsFalseAndEmptySet()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(40, 0, 0, 45, 5, 5); // clear of every seeded item
            var result = _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.All, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(itemsFound, Is.Empty);
            });
        }

        [Test]
        public void Octree_FindItemsSphere_NoIntersection_ReturnsFalseAndEmptySet()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Sphere(new Point3(40, 0, 0), 2); // clear of every seeded item
            var result = _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.All, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(itemsFound, Is.Empty);
            });
        }

        [Test]
        public void Octree_FindItems_ReturnValueTrueWhenAnyItemFound()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(1, 1, 1, 2, 2, 2);
            var result = _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(result, Is.True);
        }

        // TestItem6 carries Properties.All, so any single-flag search mask is a subset of its flags
        // and should match it regardless of how the query happens to prune the tree (a small area that
        // only touches TestItem6's own bounding box, vs one large enough to fully contain the node).
        [Test]
        public void Octree_ObjectTypeMask_PartialOverlapSearch_IncludesItemWithAllFlagsSet()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(99, 99, 99, 101, 101, 101); // overlaps TestItem6 only, far smaller than the world cube
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Has.Some.Property(nameof(TestVolumeItem.Name)).EqualTo("TestItem6"));
        }

        [Test]
        public void Octree_ObjectTypeMask_SearchAreaContainsWholeNode_IncludesItemWithAllFlagsSet()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(-1, -1, -1, 101, 101, 101); // contains the entire world cube
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Has.Some.Property(nameof(TestVolumeItem.Name)).EqualTo("TestItem6"));
        }

        // A query mask combining several types is an OR: it returns items that carry any
        // one of those types, not only items that carry all of them.
        [Test]
        public void Octree_ObjectTypeMask_CombinedMask_MatchesItemsOfEitherType()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(-1, -1, -1, 101, 101, 101); // contains the entire world cube
            int mask = (int)TestVolumeItem.Properties.Property2 | (int)TestVolumeItem.Properties.Property3;
            _Octree.GetCollidingItems(searchArea, mask, ref itemsFound);

            // TestItem2/3 (Property2), TestItem5 (Property3), TestItem6 (All) match; the Property1-only items do not
            Assert.That(itemsFound.Count, Is.EqualTo(4));
        }

        [Test]
        public void Octree_ObjectTypeMask_MaskDisjointFromItem_ExcludesItem()
        {
            var itemsFound = new List<IMapObject3d>();
            var searchArea = new Cube(4, 4, 4, 7, 7, 7); // overlaps TestItem2 (Property2) only
            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Is.Empty);
        }
    }
}
