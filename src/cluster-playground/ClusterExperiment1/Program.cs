﻿using System;
using System.Threading;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.MongoIdentityLookup;
using Proto.Remote;

namespace ClusterExperiment1
{
    public static class Program
    {
        private static async Task RunFollower()
        {
            var logger = SetupLogger();

            var cluster = SpawnMember();

            Console.ReadLine();
        }

        private static async Task RunLeader()
        {
            var logger = SetupLogger();

            var cluster = await SpawnClient();

            await Task.Delay(5000);
            
            _ = Task.Run(async () =>
                {
                    var rnd = new Random();
                    while (true)
                    {
                        try
                        {
                            var id = "myactor" + rnd.Next(0, 1000);
                            var res = await cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token
                            );

                            if (res == null)
                            {
                                logger.LogError("Null response");
                            }
                            else
                            {
                                Console.Write(".");
                            }
                        }
                        catch (Exception)
                        {
                            logger.LogError("Request timeout");
                        }
                    }
                }
            );


            Console.ReadLine();
        }

        private static ILogger SetupLogger()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole(o =>
                        {
                            o.IncludeScopes = false;
                            o.UseUtcTimestamp = false;
                            o.TimestampFormat = "hh:mm:ss:fff - ";
                        }
                    ).SetMinimumLevel(LogLevel.Information)
                )
            );
            var logger = Log.CreateLogger(nameof(Program));
            return logger;
        }


        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                await RunLeader();
            }
            else
            {
                await RunFollower();
            }
        }

        private static async Task<Cluster> SpawnClient()
        {
            var system = new ActorSystem();
            var clusterProvider = ClusterProvider();
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var identity = GetIdentityLookup();
            var cluster = new Cluster(system, serialization);
            
            var config = GetClusterConfig(clusterProvider, identity);
            await cluster.StartClientAsync(config);
            return cluster;
        }

        private static Cluster SpawnMember()
        {
            var system = new ActorSystem();
            var clusterProvider = ClusterProvider();
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var identity = GetIdentityLookup();
            var cluster = new Cluster(system, serialization);
            
            var helloProps = Props.FromProducer(() => new HelloActor());
            cluster.Remote.RegisterKnownKind("hello", helloProps);
            
            var config = GetClusterConfig(clusterProvider, identity);
            cluster.StartMemberAsync(config);
            return cluster;
        }
        
        private static ClusterConfig GetClusterConfig(ConsulProvider clusterProvider, MongoIdentityLookup identity)
        {
            return new ClusterConfig("mycluster", "127.0.0.1", 0, clusterProvider).WithIdentityLookup(identity);
        }
        
        private static ConsulProvider ClusterProvider()
        {
            return new ConsulProvider(new ConsulProviderOptions());
        }

        private static MongoIdentityLookup GetIdentityLookup()
        {
            var db = GetMongo();
            var identity = new MongoIdentityLookup("mycluster", db);
            return identity;
        }

        static IMongoDatabase GetMongo()
        {
            var connectionString =
                "mongodb://127.0.0.1:27017/ProtoMongo";
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
            var client = new MongoClient(settings);
            var database = client.GetDatabase("ProtoMongo");
            return database;
        }
    }
    
    

    public class HelloActor : IActor
    {
        //   private readonly ILogger _log = Log.CreateLogger<HelloActor>();

        public Task ReceiveAsync(IContext ctx)
        {
            if (ctx.Message is Started)
            {
                //just to highlight when this happens
                Console.Write("#");
                //_log.LogInformation("I started " + ctx.Self);
            }

            if (ctx.Message is HelloRequest)
            {
                ctx.Respond(new HelloResponse());
            }

            if (ctx.Message is Stopped)
            {
                //just to highlight when this happens
                //    _log.LogWarning("I stopped" + ctx.Self);
            }

            return Actor.Done;
        }
    }
}