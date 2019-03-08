﻿using System;
using Akka.Actor;
using Akka.Configuration;

namespace AkkaMemoryLeak
{
    class Program
    {
        static void Main(string[] args)
        {
            TestForMemoryLeak(() => CreateAndDisposeActorSystem(ConfigStringCluster));
        }

        private static void TestForMemoryLeak(Action action)
        {
            const int iterationCount = 100;
            const long memoryThreshold = 10 * 1024 * 1024;

            action();
            var memoryAfterFirstRun = GC.GetTotalMemory(true);
            Console.WriteLine($"After first run - MemoryUsage: {memoryAfterFirstRun}");

            for (var i = 1; i <= iterationCount; i++)
            {
                action();

                if (i % 10 == 0)
                {
                    var currentMemory = GC.GetTotalMemory(true);
                    Console.WriteLine($"Iteration: {i} - MemoryUsage: {currentMemory}");

                    if (currentMemory > memoryAfterFirstRun + memoryThreshold)
                        throw new InvalidOperationException("There seems to be a memory leak!");
                }
            }
        }

        /*
         * Code is largely from the comments on https://github.com/akkadotnet/akka.net/issues/2640
         *
         */

        private const string ConfigStringCluster = @"
akka {   
    actor {
        provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
    }
    remote {
        helios.tcp {
            hostname = ""127.0.0.1""
            port = 3000
        }
    }
    cluster {
        seed-nodes = [""akka.tcp://ClusterServer@127.0.0.1:3000""]
    }  
}
";

        class MyActor : ReceiveActor
        {
            public MyActor()
            {
                ReceiveAny(_ => Sender.Tell(_));
            }
        }

    private static void CreateAndDisposeActorSystem(string configString)
        {
            ActorSystem system;

            if (configString == null)
                system = ActorSystem.Create("Local");
            else
            {
                var config = ConfigurationFactory.ParseString(configString);
                system = ActorSystem.Create("Local", config);
            }

            // ensure that a actor system did some work
            var actor = system.ActorOf(Props.Create(() => new MyActor()));
            var result = actor.Ask<ActorIdentity>(new Identify(42)).Result;

            system.Terminate().Wait();
            system.Dispose();
        }
    }
}
