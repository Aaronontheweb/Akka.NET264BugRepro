using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
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
            const int iterationCount = 1000;
            const long memoryThreshold = 10 * 1024 * 1024;

            action();
            var memoryAfterFirstRun = GC.GetTotalMemory(true);
            Console.WriteLine($"After first run - MemoryUsage: {memoryAfterFirstRun}");
            var currentMemory = GC.GetTotalMemory(true);
            for (var i = 1; i <= iterationCount; i++)
            {
                action();

                if (i % 10 == 0)
                {
                    currentMemory = GC.GetTotalMemory(true);
                    Console.WriteLine($"Iteration: {i} - MemoryUsage: {currentMemory}");
                }

                if (currentMemory > memoryAfterFirstRun + memoryThreshold)
                    throw new InvalidOperationException("There seems to be a memory leak!");
            }
        }

        /*
         * Code is largely from the comments on https://github.com/akkadotnet/akka.net/issues/2640
         *
         */

        private const string ConfigStringCluster = @"
akka {   
    stdout-loglevel: DEBUG
    loglevel: DEBUG
    log-config-on-start: on

    loggers = [""Akka.Event.StandardOutLogger, Akka""]
    actor {
        debug {
            autoreceive: on
            lifecycle: on
            unhandled: on
            router-misconfiguration: on
        }
        #provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
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
                system = ActorSystem.Create("ClusterServer");
            else
            {
                var config = ConfigurationFactory.ParseString(configString);
                system = ActorSystem.Create("ClusterServer", config);
            }

            //Cluster.Get(system).RegisterOnMemberUp(() =>
            //{
            // ensure that a actor system did some work

            var actor = system.ActorOf(Props.Create(() => new MyActor()));
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            system.Scheduler.Advanced.ScheduleOnce(TimeSpan.FromMilliseconds(10), () =>
            {
                var result = actor.Ask<ActorIdentity>(new Identify(42)).Result;
                tcs.SetResult(true);
            });

            tcs.Task.Wait();
            system.Terminate();
            //});

            system.WhenTerminated.Wait();
            system.Dispose();
        }
    }
}
