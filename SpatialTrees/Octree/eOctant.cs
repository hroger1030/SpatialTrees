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

namespace SpatialTrees.Octrees
{
    /// <summary>
    /// The octants reuse EQuadrant's X/Y layout (top right, clockwise) for the near half
    /// (Z below center), then repeat the same four for the far half (Z above center).
    /// </summary>
    public enum eOctant
    {
        UpperRightNear = 0,
        LowerRightNear = 1,
        LowerLeftNear = 2,
        UpperLeftNear = 3,
        UpperRightFar = 4,
        LowerRightFar = 5,
        LowerLeftFar = 6,
        UpperLeftFar = 7
    }

    /*
             +Y
              |
              |
              7-----------4
             /|          /|
            / |         / |
           3-----------0  |
           |  |        |  |
           |  6--------|--5
           | /         | /
           |/          |/
           2-----------1----- +X
          /
         /
       -Z

        near face (Z-)        far face (Z+)
        +---+---+             +---+---+
        | 3 | 0 |             | 7 | 4 |
        +---+---+             +---+---+
        | 2 | 1 |             | 6 | 5 |
        +---+---+             +---+---+

    */
}
