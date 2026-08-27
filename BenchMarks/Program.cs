using BenchmarkDotNet.Running;

namespace BenchMarks
{
    public static class Program
    {
        /// <summary>
        /// Entry point for the spatial-tree benchmark suite. Always run in Release:
        ///
        ///   dotnet run -c Release --project BenchMarks -- --filter *Quadtree*
        ///   dotnet run -c Release --project BenchMarks -- --filter *QueryRectangle*
        ///   dotnet run -c Release --project BenchMarks -- --list flat
        ///
        /// With no arguments BenchmarkSwitcher prints an interactive menu of every
        /// benchmark class in the assembly.
        /// </summary>
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
