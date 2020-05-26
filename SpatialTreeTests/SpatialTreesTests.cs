using System.Collections.Generic;

using Geometry;
using NUnit.Framework;
using SpatialTrees;

namespace SpatialTreesTests
{
    [TestFixture]
    public class SpatialTreesTests
    {
        private Quadtree _Quadtree;

        [OneTimeSetUp]
        public void Init()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            _Quadtree.AddItem(new TestItem() { Name = "TestItem1", Location = new Point2(1, 1), ObjectTypes = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem2", Location = new Point2(5, 5), ObjectTypes = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem3", Location = new Point2(25, 25), ObjectTypes = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem4", Location = new Point2(50, 50), ObjectTypes = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem5", Location = new Point2(75, 75), ObjectTypes = (int)TestItem.Properties.Property3 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem6", Location = new Point2(100, 100), ObjectTypes = (int)TestItem.Properties.All });
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquare_Passes()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Rectangle(1, 1, 1, 1), (int)TestItem.Properties.Property1, ref items_found);

            Assert.IsTrue(items_found.Count == 1);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquareOversized_Passes()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Rectangle(-1, -1, 102, 102), (int)TestItem.Properties.Property1, ref items_found);

            Assert.IsTrue(items_found.Count == 3);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircle_Passes()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Circle(1, 1, 1), (int)TestItem.Properties.Property1, ref items_found);

            Assert.IsTrue(items_found.Count == 1);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircleOversized_Passes()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Circle(50, 50, 100), (int)TestItem.Properties.Property1, ref items_found);

            Assert.IsTrue(items_found.Count == 3);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsWithMatchingProperty_Passes()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Circle(3, 3, 5), (int)TestItem.Properties.Property2, ref items_found);

            Assert.IsTrue(items_found.Count == 1);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquareTangent_Passes()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Rectangle(0, 0, 1, 1), (int)TestItem.Properties.Property1, ref items_found);

            Assert.IsTrue(items_found.Count == 1);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircleTangent_Passes()
        {
            var items_found = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Circle(1, 2, 1), (int)TestItem.Properties.Property1, ref items_found);

            Assert.IsTrue(items_found.Count == 1);
        }
    }
}
