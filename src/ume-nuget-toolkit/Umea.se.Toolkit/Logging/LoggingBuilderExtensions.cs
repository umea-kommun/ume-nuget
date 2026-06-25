using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Umea.se.Toolkit.Configuration;
using Umea.se.Toolkit.Logging.ConsoleLogger;
using Umea.se.Toolkit.Logging.OnPremLogger;

namespace Umea.se.Toolkit.Logging;

internal static class LoggingBuilderExtensions
{
    extension(ILoggingBuilder builder)
    {
        internal ILoggingBuilder RemoveLoggers()
        {
            return builder.ClearProviders();
        }

        internal ILoggingBuilder AddCustomConsoleLogger()
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ConsoleLoggerProvider>());

            return builder;
        }

        internal ILoggingBuilder AddApplicationInsightsLogger(ApplicationConfigFunctionsBase config)
        {
            builder.Services
                .AddOpenTelemetry()
                .UseAzureMonitorExporter(options =>
                {
                    options.ConnectionString = config.ApplicationInsightsConnectionString;
                });

            return builder;
        }

        internal ILoggingBuilder AddApplicationInsightsLogger(ApplicationConfigCloudBase config)
        {
            builder.Services.AddHttpContextAccessor();

            builder.Services
                .AddOpenTelemetry()
                .UseAzureMonitor(options =>
                {
                    options.ConnectionString = config.ApplicationInsightsConnectionString;
                })
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            if (httpRequest.Headers.TryGetValue("Referer", out StringValues referer))
                            {
                                activity.SetTag("Referer", new[] { referer.ToString() });
                            }

                            if (httpRequest.Headers.TryGetValue("User-Agent", out StringValues userAgent))
                            {
                                activity.SetTag("User-Agent", userAgent.ToString());
                            }

                            if (httpRequest.Headers.TryGetValue(UserContextHeaderNames.UserId, out StringValues userId))
                            {
                                activity.SetTag("enduser.pseudo.id", userId.ToString());
                            }

                            if (httpRequest.Headers.TryGetValue(UserContextHeaderNames.SessionId, out StringValues sessionId))
                            {
                                activity.SetTag("microsoft.session.id", sessionId.ToString());
                            }
                        };
                    });
                })
                .WithLogging(logging =>
                {
                    logging.AddProcessor(sp =>
                        new UserContextLogRecordProcessor(sp.GetRequiredService<IHttpContextAccessor>()));
                });

            return builder;
        }

        internal ILoggingBuilder AddOnPremLogger(ApplicationConfigOnPremBase config)
        {
            if (config.Environment is EnvironmentNames.Local.Development &&
                string.IsNullOrWhiteSpace(config.OnPremLoggerUrl))
            {
                return builder;
            }

            builder.Services.AddHttpContextAccessor();

            builder.Services
                .AddHttpClient(nameof(OnPremLogger.OnPremLogger), httpClient =>
                {
                    httpClient.BaseAddress = new Uri(config.OnPremLoggerUrl);
                    httpClient.DefaultRequestHeaders.Add("X-Api-Key", config.OnPremLoggerKey);
                });

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, OnPremLoggerProvider>());

            return builder;
        }
    }
}
