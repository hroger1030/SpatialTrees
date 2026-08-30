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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Octree")]
    public class MultiThreadOctreeTests
    {
        private static readonly Cube World = new Cube(0, 0, 0, 1000, 1000, 1000);

        [Test]
        public void Constructor_NullInnerTree_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MultiThreadOctree((Octree)null));
        }

        [Test]
        public void AddItem_And_Count_DelegateToInnerTree()
        {
            using var tree = new MultiThreadOctree(World, 6, 4);

            tree.AddItem(new TestVolumeItem("a", 10, 10, 10, (int)TestVolumeItem.Properties.Property1));
            tree.AddItem(new TestVolumeItem("b", 20, 20, 20, (int)TestVolumeItem.Properties.Property1));

            Assert.That(tree.Count, Is.EqualTo(2));
        }

        [Test]
        public void AddItems_InsertsEveryItemUnderOneLock()
        {
            using var tree = new MultiThreadOctree(World, 8, 8);
            var items = MakePopulation(300, seed: 7);

            tree.AddItems(items);

            Assert.That(tree.Count, Is.EqualTo(300));
        }

        [Test]
        public void RemoveItem_ReturnsInnerResult_AndDropsTheItem()
        {
            using var tree = new MultiThreadOctree(World);
            var item = new TestVolumeItem("x", 5, 5, 5, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(item);

            Assert.Multiple(() =>
            {
                Assert.That(tree.RemoveItem(item), Is.True);
                Assert.That(tree.RemoveItem(item), Is.False);
                Assert.That(tree.Count, Is.EqualTo(0));
            });
        }

        [Test]
        public void GetCollidingItems_FindsOverlappingItems()
        {
            using var tree = new MultiThreadOctree(World, 8, 4);
            var target = new TestVolumeItem("hit", 100, 100, 100, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(target);
            tree.AddItem(new TestVolumeItem("miss", 900, 900, 900, (int)TestVolumeItem.Properties.Property1));

            List<IMapObject3d> found = null;
            bool any = tree.GetCollidingItems(new Cube(95, 95, 95, 105, 105, 105), (int)TestVolumeItem.Properties.All, ref found);

            Assert.Multiple(() =>
            {
                Assert.That(any, Is.True);
                Assert.That(found, Does.Contain(target));
                Assert.That(found, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Build_ReturnsWrappedBulkLoadedTree()
        {
            var items = MakePopulation(400, seed: 3);

            using var tree = MultiThreadOctree.Build(World, 8, 16, items);

            Assert.That(tree.Count, Is.EqualTo(400));
        }

        [Test]
        public void Write_Delegate_MutatesInnerTree()
        {
            using var tree = new MultiThreadOctree(World);

            tree.Write(t => t.AddItem(new TestVolumeItem("d", 1, 1, 1, (int)TestVolumeItem.Properties.Property1)));

            Assert.That(tree.Read(t => t.ObjectIndex.Count), Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentReadersAndWriters_DoNotCorruptTheTree()
        {
            using var tree = new MultiThreadOctree(World, 8, 8);
            var items = MakePopulation(500, seed: 11);
            tree.AddItems(items);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                var rng = new Random(Environment.CurrentManagedThreadId);
                List<IMapObject3d> found = null;
                while (!cts.IsCancellationRequested)
                {
                    float x = rng.Next(0, 1000);
                    float y = rng.Next(0, 1000);
                    float z = rng.Next(0, 1000);
                    tree.GetCollidingItems(new Cube(x, y, z, x + 40, y + 40, z + 40), (int)TestVolumeItem.Properties.All, ref found);
                }
            }));

            var writers = Enumerable.Range(0, 2).Select(w => Task.Run(() =>
            {
                var rng = new Random(1000 + w);
                while (!cts.IsCancellationRequested)
                {
                    var victim = items[rng.Next(items.Count)];
                    victim.Location = new Point3(rng.Next(0, 1000), rng.Next(0, 1000), rng.Next(0, 1000));
                    tree.MoveItem(victim);
                }
            }));

            Assert.DoesNotThrow(() => Task.WaitAll(readers.Concat(writers).ToArray()));
            Assert.That(tree.Count, Is.EqualTo(500));
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var tree = new MultiThreadOctree(World);

            Assert.DoesNotThrow(() =>
            {
                tree.Dispose();
                tree.Dispose();
            });
        }

        private static List<IMapObject3d> MakePopulation(int count, int seed)
        {
            var rng = new Random(seed);
            var list = new List<IMapObject3d>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(new TestVolumeItem($"i{i}",
                    rng.Next(0, 1000), rng.Next(0, 1000), rng.Next(0, 1000),
                    (int)TestVolumeItem.Properties.Property1));
            }

            return list;
        }
    }
}
