using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Text.Json.Serialization;
using AppKettle.Filters;
using AppKettle.Service.Managers;

namespace AppKettle
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var config = Configuration.Get<AppConfig>();

            services.Configure<AppConfig>(Configuration);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardLimit = null;
                options.ForwardedHeaders = ForwardedHeaders.All;
            });

            services.AddApiVersioning(o =>
            {
                o.ReportApiVersions = true;
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.DefaultApiVersion = new ApiVersion(1, 0);
            });

            services.AddRouting(option => { option.LowercaseUrls = true; });

            services.AddMvc()
                .AddJsonOptions(opts => { opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

            services.AddControllers(options => options.Filters.Add(new HttpResponseExceptionFilter()));

            services.AddSingleton<KettleManager>();
  
            services.AddCors();

            if (config.Swagger?.Enabled == true)
                services.AddSwaggerGen(c => { c.SwaggerDoc("v1", CreateOpenApiInfo()); });


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            var config = Configuration.Get<AppConfig>();

            if (config.LogAllHeaders)
            {
                app.Use(async (context, next) =>
                {
                    var rq = context.Request;
                    // Request method, scheme, and path
                    logger.LogInformation(
                        $"Request {rq.Scheme} {rq.Path} {rq.Method} (from {context.Connection.RemoteIpAddress})");

                    // Headers
                    foreach (var (key, value) in rq.Headers)
                    {
                        logger.LogInformation($"Header: {key}: {value}");
                    }

                    await next();
                });
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }


            if (config.Swagger?.Enabled == true)
            {
                // Enable middleware to serve generated Swagger as a JSON endpoint.
                app.UseSwagger(c => { c.RouteTemplate = "/kettle/swagger/{documentname}/swagger.json"; });

                // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
                // specifying the Swagger JSON endpoint.
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/kettle/swagger/v1/swagger.json", "AppKettle Service API V1");
                    c.RoutePrefix = "kettle";
                });
            }

            var allowedHosts = config.AllowedHosts ?? Array.Empty<string>();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public static OpenApiInfo CreateOpenApiInfo()
        {
            var info = new OpenApiInfo
            {
                Version = "v1",
                Title = "LewKirk AppKettle Service API V1",
                Description = "REST service to interface with AppKettle kettles",
                License = new OpenApiLicense
                {
                    Name = "Apache 2.0",
                    Url = new Uri("https://www.apache.org/licenses/LICENSE-2.0.html")
                }
            };
            return info;
        }
    }
}
