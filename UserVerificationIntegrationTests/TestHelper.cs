﻿using demofluffyspoon.contracts;
using demofluffyspoon.contracts.Models;
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
using System;
using System.Collections.Generic;

namespace UserVerificationIntegrationTests
{
    public class TestHelper
    {
        public TestCluster GenerateTestCluster<T>()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurations<T>>();
            builder.AddClientBuilderConfigurator<TestClientConfigurations>();
            builder.Options.InitialSilosCount = 1;
            
            TestCluster cluster = builder.Build();
            cluster.Deploy();

            return cluster;
        }

        private class TestSiloConfigurations<T> : ISiloBuilderConfigurator {
            
            private readonly IHost _host;

            public TestSiloConfigurations()
            {
                _host = new HostBuilder()
                    .ConfigureServices((ctx, services) =>
                    {
                        services
                            .AddKVStores<HashSet<string>>()
                            .FromJsonFile(ctx.Configuration.GetSection("BlacklistedEmails"))
                            .AddMemoryDataStore();
                    })
                    .ConfigureAppConfiguration(x => x.AddJsonFile("appsettings.json")).Build();
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
                
                hostBuilder.ConfigureApplicationParts(parts =>
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
                        options.ConsumerGroupId = typeof(T).Name;
                        options.ConsumeMode = ConsumeMode.LastCommittedMessage;
                        options.AddTopic(nameof(UserVerificationEvent), topicConfiguration);
                        options.AddTopic(nameof(UserRegisteredEvent), topicConfiguration);
                    }).Build();
            }
        }
    
        public class TestClientConfigurations : IClientBuilderConfigurator {
            public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
            {
//                clientBuilder.AddKafka(Constants.StreamProviderName)
//                    .WithOptions(options =>
//                    {
//                        options.FromConfiguration(configuration);
//                        options.AddTopic(nameof(UserVerificationEvent));
//                        options.AddTopic(nameof(UserRegisteredEvent));
//                    });
            }
        }
    }
}