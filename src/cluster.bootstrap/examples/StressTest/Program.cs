using System;
using System.Threading.Tasks;

namespace StressTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int clusterSize = 30;
            int scaledDownsize = 5;
            if (args.Length > 0)
            {
                var arg = args[0].ToLowerInvariant();
                if(arg.Equals("--help") || arg.Equals("-h") || arg.Equals("/h"))
                {
                    Usage();
                    return;
                }
                if(!int.TryParse(arg, out clusterSize) || clusterSize < 2)
                {
                    Console.WriteLine("First argument must be an integer and greater than 1.");
                    Usage();
                    return;
                }
            }
            if(args.Length > 1)
            {
                if(!int.TryParse(args[1], out scaledDownsize) || scaledDownsize < 1)
                {
                    Console.WriteLine("Second argument must be an integer and greater than 0.");
                    Usage();
                    return;
                }
                if(scaledDownsize >= clusterSize)
                {
                    Console.WriteLine("Second argument must be smaller than the first argument.");
                    Usage();
                    return;
                }
            } else if(scaledDownsize >= clusterSize)
            {
                scaledDownsize = clusterSize - 1;
            }
            
            Console.WriteLine($"Running stress test with initial cluster size {clusterSize} and scaling down to {scaledDownsize}.");
            var test = new StressTest(clusterSize, scaledDownsize);
            await test.StartTest();
        }

        static void Usage()
        {
            Console.WriteLine(@"
StressTest

This simple stress test will attempt to:
  - Create an Akka.NET cluster of a certain size using Akka.Management.Cluster.Bootstrap to 
    wire the cluster together, and
  - Scale down the cluster down in a rapid manner.

Usage: 
    StressTest.exe [cluster-size] [scaled-down-size]

cluster-size     : Size of the initial cluster the stress test will try to form. Default: 30, minimum of 2
scaled-down-size : Size of the final cluster to scale down to after initial cluster has formed.
                   Must be smaller than cluster-size. Default: 5, minimum of 1");
        }
    }
}