using System;
using System.Diagnostics;
using System.IO;
using AppKettle.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers.Wrappers;
using NLog.Layouts;
using NLog.Targets;
using NLog.Web;
using ILogger = NLog.ILogger;
using LogLevel = NLog.LogLevel;

namespace AppKettle
{
    public class Program
    {
        #region Constants

        private static readonly string ProgramDataConfigFilePath;
        private static readonly string ProgramDataDirectory;
        private static readonly string BaseConfigFilePath;
        private static readonly string WorkingConfigFilePath;
        private static readonly string WorkingDirectory;

        #endregion

        #region Constructors

        static Program()
        {
            WorkingDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? string.Empty;

            if (OperatingSystem.IsWindows())
            {
                ProgramDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "LewKirk\\AppKettle");
            }
            else
            {
                ProgramDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LewKirk/AppKettle");
            }

#if DEBUG
            const string configFilename = "appsettings.Development.json";
#else
            const string configFilename = "appsettings.json";
#endif

            BaseConfigFilePath = Path.Combine(AppContext.BaseDirectory, configFilename);
            WorkingConfigFilePath = Path.Combine(WorkingDirectory, configFilename);
            ProgramDataConfigFilePath = Path.Combine(ProgramDataDirectory, configFilename);
        }

        #endregion

        public static void Main(string[] args)
        {
            var logger = InitializeLogger();

            //everyone likes a bit more window space :)
            if (OperatingSystem.IsWindows() && Console.LargestWindowWidth > 0)
            {
                try
                {
                    Console.SetWindowSize((int)(Console.LargestWindowWidth * .8),
                    (int)(Console.LargestWindowHeight * .8));
                }
                catch (Exception ex)
                {
                    logger.Info($"Failed to increase window size (safe to ignore) - reason: {ex.Message}");
                }
            }

            logger.Info("----------------------------------------");
            logger.Info($"{Product.Name}: {Product.Version} (Built: {Product.BuildTime})");
            logger.Info($"Executable directory: {WorkingDirectory}");
            logger.Info($"Operating system: {Environment.OSVersion.Platform} ({Environment.OSVersion.VersionString})");
            var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LewKirk", "AppKettle");
            logger.Info($"Application data directory: {appDir}");

            try
            {
                PrepareConfigFile(logger);

                logger.Info($"Configuration running from {ProgramDataConfigFilePath}");

                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Stopped program because of exception");
                throw;
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                LogManager.Shutdown();
            }

        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var appConfig = LoadConfiguration(ProgramDataConfigFilePath);

            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    if (appConfig.ApplicationUrls != null)
                        webBuilder.UseUrls(appConfig.ApplicationUrls);
                })
                .ConfigureAppConfiguration(configHost =>
                {
                    configHost.AddJsonFile(ProgramDataConfigFilePath, true);
                    configHost.AddEnvironmentVariables("LEWKIRKAPPKETTLE_");
                    configHost.AddCommandLine(args);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                })
                .UseNLog();

        }


        public static void PrepareConfigFile(ILogger logger)
        {
            if (ProgramDataConfigFilePath != null && File.Exists(ProgramDataConfigFilePath))
            {
                if (File.Exists(WorkingConfigFilePath) && File.GetLastWriteTime(WorkingConfigFilePath) > File.GetLastWriteTime(ProgramDataConfigFilePath))
                {
                    if (File.Exists(ProgramDataConfigFilePath))
                    {
                        logger.Warn("There was a problem reading the settings file, resetting to defaults");
                        var programDataConfigFolder = Path.GetDirectoryName(ProgramDataConfigFilePath);
                        if (ProgramDataConfigFilePath != null && Directory.Exists(programDataConfigFolder))
                        {
                            var backupFileSettingsName = $"{Path.GetFileName(ProgramDataConfigFilePath)}-backup_{DateTime.UtcNow.ToFileTimeUtc()}";
                            logger.Info($"Problematic settings file has been copied to: {backupFileSettingsName}");
                            File.Move(ProgramDataConfigFilePath!, Path.Combine(programDataConfigFolder!, backupFileSettingsName));
                        }
                    }

                    logger.Warn($"Performing import of newer settings from '{WorkingConfigFilePath}' file");
                    File.Copy(WorkingConfigFilePath, ProgramDataConfigFilePath);
                }
            }
            else
            {
                if (!Directory.Exists(ProgramDataDirectory))
                    Directory.CreateDirectory(ProgramDataDirectory);

                if (File.Exists(WorkingConfigFilePath))
                {
                    logger.Warn($"Performing initial import of settings from '{WorkingConfigFilePath}' file to {ProgramDataConfigFilePath}");
                    File.Copy(WorkingConfigFilePath, ProgramDataConfigFilePath!);
                }
                else
                {
                    logger.Warn($"Performing import of default settings from '{BaseConfigFilePath}' file");
                    File.Copy(BaseConfigFilePath, ProgramDataConfigFilePath!);
                }
            }
        }

        private static AppConfig LoadConfiguration(string filepath)
        {
            var configBuilder = new ConfigurationBuilder();
            var config = configBuilder.AddJsonFile(filepath, false).Build();
            return config.Get<AppConfig>();
        }


        private static ILogger InitializeLogger()
        {
            var configFile = Path.Combine(WorkingDirectory, "nlog.appConfig");

            if (File.Exists(configFile))
                LogManager.LoadConfiguration(configFile);

            if (LogManager.Configuration == null)
            {
                LogManager.Configuration = new LoggingConfiguration();
                ConfigurationItemFactory.Default.LayoutRenderers.RegisterDefinition("pad", typeof(PaddingLayoutRendererWrapper));

                var layout = new SimpleLayout
                {
                    Text = "${longdate} ${pad:padding=-10:inner=(${level:upperCase=true})} " +
                           "${pad:padding=-20:fixedLength=true:inner=${logger:shortName=true}} " +
                           "${message} ${exception:format=tostring}"
                };

                var consoleTarget = new ColoredConsoleTarget
                {
                    UseDefaultRowHighlightingRules = true,
                    DetectConsoleAvailable = true,
                    Layout = layout
                };

                var fileTarget = new FileTarget
                {
                    FileName = Path.Combine(ProgramDataDirectory, "logs", "service.log"),
                    KeepFileOpen = false,
                    ArchiveEvery = FileArchivePeriod.Day,
                    Layout = layout
                };



                LogManager.Configuration.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);
                LogManager.Configuration.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);
            }

            LogManager.ReconfigExistingLoggers();
            return LogManager.GetCurrentClassLogger();
        }
    }
}
