# Spatial Trees

This library is designed to allow you to very quickly generate and search a quattree, a 2d spatial index. The quadtree is optimized to allow 
fast inserts and updates of objects, making it ideal for real time game and physics applications. The project includes a reference to another
library I wrote to support basic geometric operations, available on this github account under the name of 'geometry'. 

## Init a spatial index

To initialize a new QuadTree, use the following code:

```
var boundingBox = new Rectangle(0,0,1000,1000);
var maxDepth = 5;
var maxObjects = 100;
var tree = new Quadtree(boundingBox, maxDepth, maxObjects)
```

The bounding box is the outer boundary of the search space. The depth is the number of levels of 'resolution'. The more levels you add, the 
the more the space is subdivided, the more memory is consumed. The max objects is an artificial limiter that will set the upper limit of the 
number of objects that can be added. Set this as big as you want if you have a lot of RAM or CPU to burn. 

The search uses binary space partitioning, which is very, very fast. The objects are also indexed so that moving them in the index can be 
done very quickly too. 

The following utility methods are available on a quadtree:

```
Resize() - Changes the outer bounding box, adding a new top level node that is twice as big as the current top level node.

AddItem(IMapObject item) - Adds an item to the tree.

MoveItem(IMapObject item) - Moves item in tree. Does checks for collisions. Returns true if the item was moved, false if it could not be moved.

RemoveItem(IMapObject item) - removes the specified item.

Clear() - clears all items from the tree.

GetCollidingItems(Rectangle collisionBox, int objectTypes, ref HashSet<IMapObject> itemsFound) - returns a list of unique items that are colliding with the rectangle that is passed in.

GetCollidingItems(Circle collisionCircle, int objectProperties, ref HashSet<IMapObject> itemsFound) - returns a list of unique items that are colliding with the circle that is passed in.
```

## Items

The quadtree works with any object that implements an 'IMapObject' interface: 

```   
    public interface IMapObject
    {
        int ObjectTypes { get; set; }
        Point2 Location { get; set; }
        Rectangle BoundingBox { get; }
    }
```

As long as your objects implement these properties, you can add anything you want to the structure with little effort. The object types property is intended to allow differing types of
objects to be intermixed and selectively filtered in searches using bit flags. 

## Octree

I have an implementation of a three dimensional data structure that functions very similarly to the two dimensional structure. I have not included it with this package yet, as writing proper
unittests to validate that it works properly is rather time consuming. Please contact me directly if you want access to this data structure, and I can make it available to you.

## License

This library is covered with the MIT license, and you can do pretty much anything you want with it, except claim it as your own work. Go build something cool with this and sell it for a lot 
of money. 
