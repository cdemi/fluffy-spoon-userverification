using demofluffyspoon.contracts;
using fluffyspoon.userverification.Grains;
using GiG.Core.Data.KVStores.Abstractions;
using GiG.Core.Data.KVStores.Extensions;
using GiG.Core.Data.KVStores.Providers.FileProviders.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using System;
using System.Collections.Generic;

namespace UserVerificationComponentTests
{
    public class ClusterFixture : IDisposable
    {
        public TestCluster Cluster { get; }

        public ClusterFixture()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurations>();
            builder.AddClientBuilderConfigurator<TestClientConfigurations>();

            Cluster = builder.Build();
            Cluster.Deploy();
        }

        public void Dispose() => Cluster.StopAllSilos();
    }
    
    public class TestSiloConfigurations : ISiloBuilderConfigurator {
        private IHost _host;

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
            hostBuilder.AddSimpleMessageStreamProvider(Constants.StreamProviderName,
                    options => options.FireAndForgetDelivery = true)
                .AddMemoryGrainStorage("PubSubStore")
                .AddMemoryGrainStorageAsDefault()
                .ConfigureApplicationParts(parts =>
                {
                    parts.AddApplicationPart(typeof(UserVerificationGrain).Assembly).WithReferences();
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton(x => _host.Services.GetService<IDataRetriever<HashSet<string>>>());
                });
        }
    }
    
    public class TestClientConfigurations : IClientBuilderConfigurator {
        public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
        {
            clientBuilder.AddSimpleMessageStreamProvider(Constants.StreamProviderName, options => options.FireAndForgetDelivery = true);
        }
    }
    
}

