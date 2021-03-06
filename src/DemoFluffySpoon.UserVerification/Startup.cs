using System.Collections.Generic;
using DemoFluffySpoon.Contracts;
using DemoFluffySpoon.Contracts.Models;
using DemoFluffySpoon.UserVerification.Grains;
using GiG.Core.Data.KVStores.Extensions;
using GiG.Core.Data.KVStores.Providers.FileProviders.Extensions;
using GiG.Core.DistributedTracing.Web.Extensions;
using GiG.Core.HealthChecks.Extensions;
using GiG.Core.Hosting.Extensions;
using GiG.Core.Orleans.Clustering.Consul.Extensions;
using GiG.Core.Orleans.Clustering.Extensions;
using GiG.Core.Orleans.Clustering.Kubernetes.Extensions;
using GiG.Core.Orleans.Silo.Extensions;
using GiG.Core.Orleans.Streams.Kafka.Extensions;
using GiG.Core.Web.Hosting.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using Orleans.Streams.Kafka.Config;
using OrleansDashboard;
using HostBuilderContext = Microsoft.Extensions.Hosting.HostBuilderContext;

namespace DemoFluffySpoon.UserVerification
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Info Management
            services.ConfigureInfoManagement(Configuration);

            // Health Checks
            services.ConfigureHealthChecks(Configuration)
                .AddHealthChecks();

            // Forwarded Headers
            services.ConfigureForwardedHeaders();
            
            services
                .AddKVStores<HashSet<string>>()
                .FromJsonFile(Configuration.GetSection("BlacklistedEmails"))
                .AddMemoryDataStore();
        }

        // This method gets called by the runtime. Use this method to configure Orleans.
        public static void ConfigureOrleans(HostBuilderContext ctx, ISiloBuilder builder)
        {
            var configuration = ctx.Configuration;
            var topicConfiguration = new TopicCreationConfig
            {
                AutoCreate = true,
                Partitions = 8
            };
            
            builder.ConfigureCluster(configuration)
                .UseDashboard(x => x.HostSelf = false)
                .ConfigureEndpoints()
                .AddAssemblies(typeof(UserVerificationGrain))
                .AddKafka(Constants.StreamProviderName)
                .WithOptions(options =>
                {
                    options.FromConfiguration(ctx.Configuration);
                    options.AddTopic(nameof(UserVerificationEvent), topicConfiguration);
                    options.AddTopic(nameof(UserRegisteredEvent), topicConfiguration);
                })
                .AddJson()
                .Build()
                .AddMemoryGrainStorage("PubSubStore")
                .AddMemoryGrainStorageAsDefault()
                .UseMembershipProvider(configuration, x =>
                {
                    x.ConfigureConsulClustering(configuration);
                    x.ConfigureKubernetesClustering(configuration);
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders();
            app.UsePathBaseFromConfiguration();
            app.UseCorrelation();
            app.UseHealthChecks();
            app.UseInfoManagement();
            app.UseOrleansDashboard(new DashboardOptions { BasePath = "/dashboard" });
        }
    }
}