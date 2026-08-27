using System;
using Geometry;

namespace BenchMarks
{
    /// <summary>
    /// Deterministic world bounds, item positions and query volumes shared by every
    /// benchmark, so a run is directly comparable to the one before it across code
    /// changes. Same seed => identical layout every time.
    /// </summary>
    public static class WorldData
    {
        public const float WorldSize = 10_000f;
        public const int Seed = 20260826;

        /// <summary>Object-type mask that matches every benchmark item.</summary>
        public const int AllTypes = ~0;

        public static Rectangle World2d()
        {
            return new Rectangle(0f, 0f, WorldSize, WorldSize);
        }

        public static Cube World3d()
        {
            return new Cube(0f, 0f, 0f, WorldSize, WorldSize, WorldSize);
        }

        public static Point2[] Points2d(int count)
        {
            var rng = new Random(Seed);
            var points = new Point2[count];

            for (int i = 0; i < count; i++)
                points[i] = new Point2(NextCoord(rng), NextCoord(rng));

            return points;
        }

        public static Point3[] Points3d(int count)
        {
            var rng = new Random(Seed);
            var points = new Point3[count];

            for (int i = 0; i < count; i++)
                points[i] = new Point3(NextCoord(rng), NextCoord(rng), NextCoord(rng));

            return points;
        }

        public static Rectangle[] QueryRects(int count, float size)
        {
            var rng = new Random(Seed + 1);
            var rects = new Rectangle[count];

            for (int i = 0; i < count; i++)
                rects[i] = new Rectangle(NextOrigin(rng, size), NextOrigin(rng, size), size, size);

            return rects;
        }

        public static Circle[] QueryCircles(int count, float radius)
        {
            var rng = new Random(Seed + 2);
            var circles = new Circle[count];

            for (int i = 0; i < count; i++)
                circles[i] = new Circle(NextCoord(rng), NextCoord(rng), radius);

            return circles;
        }

        public static Cube[] QueryCubes(int count, float size)
        {
            var rng = new Random(Seed + 1);
            var cubes = new Cube[count];

            for (int i = 0; i < count; i++)
            {
                float x = NextOrigin(rng, size);
                float y = NextOrigin(rng, size);
                float z = NextOrigin(rng, size);
                cubes[i] = new Cube(x, y, z, x + size, y + size, z + size);
            }

            return cubes;
        }

        public static Sphere[] QuerySpheres(int count, float radius)
        {
            var rng = new Random(Seed + 2);
            var spheres = new Sphere[count];

            for (int i = 0; i < count; i++)
                spheres[i] = new Sphere(new Point3(NextCoord(rng), NextCoord(rng), NextCoord(rng)), radius);

            return spheres;
        }

        // keep a 1-unit margin so a unit-sized item's whole box stays inside the world
        public static float NextCoord(Random rng)
        {
            return 1f + (float)rng.NextDouble() * (WorldSize - 2f);
        }

        public static float NextOrigin(Random rng, float size)
        {
            return (float)rng.NextDouble() * (WorldSize - size);
        }
    }
}
