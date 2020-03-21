using System;
using System.Collections.Generic;
using DemoFluffySpoon.Contracts;
using DemoFluffySpoon.Contracts.Models;
using GiG.Core.Data.KVStores.Abstractions;
using GiG.Core.Data.KVStores.Extensions;
using GiG.Core.Data.KVStores.Providers.FileProviders.Extensions;
using GiG.Core.Orleans.Streams.Kafka.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Orleans.Streams.Kafka.Config;
using Orleans.TestingHost;
using Polly;
using Polly.Retry;

namespace DemoFluffySpoon.UserVerification.Tests.Integration
{
    public class TestHelper
    {
        private static Guid _runId;

        public static AsyncRetryPolicy<bool> Polly;

        public TestHelper()
        {
            _runId = Guid.NewGuid();
            Polly = Policy.HandleResult<bool>(x => !x)
                .WaitAndRetryAsync(20, retryAttempt => TimeSpan.FromMilliseconds(1000));
        }
        
        public TestCluster GenerateTestCluster<T>()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurations<T>>();
            builder.Options.InitialSilosCount = 1;
            
            var cluster = builder.Build();
            cluster.Deploy();

            return cluster;
        }

        private class TestSiloConfigurations<T> : ISiloBuilderConfigurator {
            
            private readonly IHost _host;

            public TestSiloConfigurations()
            {
                _host = Host.CreateDefaultBuilder()
                    .ConfigureServices((ctx, services) =>
                    {
                        services
                            .AddKVStores<HashSet<string>>()
                            .FromJsonFile(ctx.Configuration.GetSection("BlacklistedEmails"))
                            .AddMemoryDataStore();
                        
                    })
                    .Build();

                _host.Start();
            }

            public void Configure(ISiloHostBuilder hostBuilder)
            {
                hostBuilder.GetConfiguration();
                
                var topicConfiguration = new TopicCreationConfig
                {
                    AutoCreate = true,
                    Partitions = 1
                };
                
                hostBuilder
                    .ConfigureApplicationParts(parts =>
                    {
                        parts.AddApplicationPart(typeof(T).Assembly).WithReferences();
                    })
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddSingleton(x => _host.Services.GetService<IDataRetriever<HashSet<string>>>());
                    })
                    .AddMemoryGrainStorage("PubSubStore")
                    .AddMemoryGrainStorageAsDefault()
                    .AddKafka(Constants.StreamProviderName)
                    .WithOptions(options =>
                    {
                        options.FromConfiguration(_host.Services.GetService<IConfiguration>());
                        options.ConsumerGroupId = typeof(T).Name + _runId;
                        options.ConsumeMode = ConsumeMode.LastCommittedMessage;
                        options.AddTopic(nameof(UserVerificationEvent), topicConfiguration);
                        options.AddTopic(nameof(UserRegisteredEvent), topicConfiguration);
                    }).Build();
            }
        }
    }
}