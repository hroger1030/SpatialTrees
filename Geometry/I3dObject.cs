namespace Geometry
{
    public interface I3dObject
    {
        float Volume();
        float SurfaceArea();
        bool Intersects(I3dObject other);
        bool Contains(Point3 point3);
    }
}