using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace SearchAggregator
{
    public class ProxySettings
    {
        public class Entry {
            public string Name { get; set; }
            public Uri Uri { get; set; }
            public Uri SearchEndpoint { get; set; }
        }
        public List<Entry> PackageSources { get; set; }
    }

    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                //.AddJsonFile("ProxySettings.json")
                ;
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.Configure<ProxySettings>(Configuration.GetSection("ProxySettings"));

            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                //app.UseDe
            }

            app.UseIISPlatformHandler();

            app.UseStaticFiles();

            app.UseMvc();

            //var packageSources = Configuration.GetSection("PackageSources").GetChildren().Select(c => c["url"]).ToList();
        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
