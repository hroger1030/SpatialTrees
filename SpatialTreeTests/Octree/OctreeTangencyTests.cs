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
    // Single-item fixture with hand-picked geometry so the "tangent" cases touch the item's
    // bounding box at exactly zero area / exactly at the radius, rather than genuinely overlapping it.
    [TestFixture]
    [Category("Octree")]
    public class OctreeTangencyTests
    {
        private Octree _Octree;
        private TestVolumeItem _Item;

        [OneTimeSetUp]
        public void Init()
        {
            _Octree = new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 10);
            _Item = new TestVolumeItem("Target", 10, 10, 10, (int)TestVolumeItem.Properties.Property1); // bounding box (9.5,9.5,9.5)-(10.5,10.5,10.5)
            _Octree.AddItem(_Item);
        }

        [Test]
        public void Cube_TouchingExactlyOnRightFace_IsFound()
        {
            var itemsFound = new HashSet<IMapObject3d>();
            var searchArea = new Cube(7.5f, 9.5f, 9.5f, 9.5f, 10.5f, 10.5f); // right face at x=9.5, spans the full y/z-range of the item's box

            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(_Item));
        }

        [Test]
        public void Cube_JustShortOfTouching_IsNotFound()
        {
            var itemsFound = new HashSet<IMapObject3d>();
            var searchArea = new Cube(7.5f, 9.5f, 9.5f, 9.499f, 10.5f, 10.5f); // right face at x=9.499, just short of the item's left face

            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Is.Empty);
        }

        [Test]
        public void Sphere_DistanceExactlyEqualToRadius_IsFound()
        {
            var itemsFound = new HashSet<IMapObject3d>();
            var searchArea = new Sphere(new Point3(8.5f, 10f, 10f), 1f); // closest point on item's box is (9.5,10,10): exactly 1 unit away

            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(_Item));
        }

        [Test]
        public void Sphere_DistanceJustBeyondRadius_IsNotFound()
        {
            var itemsFound = new HashSet<IMapObject3d>();
            var searchArea = new Sphere(new Point3(8.499f, 10f, 10f), 1f); // closest point is just over 1 unit away

            _Octree.GetCollidingItems(searchArea, (int)TestVolumeItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Is.Empty);
        }
    }
}
