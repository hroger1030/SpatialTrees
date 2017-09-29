//////////////////////////////////////////////////////////////////////////////////
// Copyright (C) Global Conquest Games, LLC - All Rights Reserved               //
// Unauthorized copying of this file, via any medium is strictly prohibited     //
// Proprietary and confidential                                                 //
//////////////////////////////////////////////////////////////////////////////////

using Geometry;

namespace SpatialTrees
{
    public interface IMapObject
    {
        int ObjectTypes { get; set; }
        Point2 Location { get; set; }
        Rectangle BoundingBox { get; }
    }
}