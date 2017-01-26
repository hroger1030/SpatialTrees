namespace Geometry
{
    public interface I2dObject
    {
        float perimeter();
        float Area();
        bool Intersects(I2dObject other);
        bool Contains(Point2 point3);
    }
}