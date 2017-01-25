using System;
using System.Configuration;
using System.Diagnostics;

namespace TestApp
{
    public class Program
    {
        private static readonly string _Conn = ConfigurationManager.AppSettings["SQLConnection"];
        private static Stopwatch _Timer;

        static void Main(string[] args)
        {
            _Timer = new Stopwatch();
            _Timer.Start();



            _Timer.Stop();
            Console.WriteLine(string.Format("Operation took {0} milliseconds to complete.", _Timer.ElapsedMilliseconds));
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(); 
        }
    }
}


