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

namespace SpatialTrees.Quadtrees
{
    /// <summary>
    /// An object the quadtree can index. The tree positions and range-checks items purely
    /// by <see cref="BoundingBox"/> (routing uses its center); <see cref="Location"/> is a
    /// caller convenience and should stay consistent with the box's center.
    /// The <see cref="BoundingBox"/> must have ordered coordinates (Left &lt;= Right,
    /// Top &lt;= Bottom); an inverted rectangle is not validated and routes incorrectly.
    /// </summary>
    public interface IMapObject2d
    {
        int ObjectType { get; set; }
        Point2 Location { get; set; }
        Rectangle BoundingBox { get; }
    }
}