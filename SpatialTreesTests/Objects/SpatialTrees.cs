using System.Collections.Generic;

using Geometry;
using NUnit.Framework;
using SpatialTrees;

namespace SpatialTreesTests
{
    [TestFixture]
    public class SpatialTreesTests
    {
        Quadtree _Quadtree;

        [OneTimeSetUp]
        public void GenerateRandInt()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            _Quadtree.AddItem(new TestItem() { Name = "TestItem1", Location = new Point2(1, 1), ObjectProperties = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem2", Location = new Point2(5, 5), ObjectProperties = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem3", Location = new Point2(25, 25), ObjectProperties = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem4", Location = new Point2(50, 50), ObjectProperties = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem5", Location = new Point2(75, 75), ObjectProperties = (int)TestItem.Properties.Property3 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem6", Location = new Point2(100, 100), ObjectProperties = (int)TestItem.Properties.All });
        }

        [Test]
        [Category("Quadtree")]
        public void FindItems()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Circle(4, 4, 5), (int)TestItem.Properties.Property1, ref items_found);

            Assert.IsTrue(items_found.Count == 1);
        }
    }
}
