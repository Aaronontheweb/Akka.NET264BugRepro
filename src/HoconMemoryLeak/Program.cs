using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Configuration;

namespace HoconMemoryLeak
{
    class Program
    {
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

        static void Main(string[] args)
        {
            Config config = null;
            for (var i = 0; i < 10000; i++)
            {
                config = ConfigurationFactory.ParseString(ConfigStringCluster)
                    .WithFallback(Akka.Cluster.Sharding.ClusterSharding.DefaultConfig());
                var settings = new Settings(null, config);
            }

            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}
