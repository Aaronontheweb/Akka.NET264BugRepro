using System;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Persistence;

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
        default-dispatcher.shutdown-timeout = 0s
        debug {
            autoreceive: on
            lifecycle: on
            unhandled: on
            router-misconfiguration: on
        }
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
    akka.persistence.dispatchers{
        default-plugin-dispatcher = ""akka.actor.default-dispatcher""
        default-replay-dispatcher = ""akka.actor.default-dispatcher""
        default-stream-dispatcher = ""akka.actor.default-dispatcher""
    }
}
";

        class MyActor : ReceivePersistentActor
        {
            public MyActor()
            {
                CommandAny(_ => Sender.Tell(_));
            }

            public override string PersistenceId => Context.Self.Path.Name;
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

            Cluster.Get(system).RegisterOnMemberUp(() =>
            {
                // ensure that a actor system did some work
                var actor = system.ActorOf(Props.Create(() => new MyActor()));
                var result = actor.Ask<ActorIdentity>(new Identify(42)).Result;
                system.Terminate();
            });

            system.WhenTerminated.Wait();
            system.Dispose();
        }
    }
}
