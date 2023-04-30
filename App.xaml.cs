using System;
using System.Windows;
using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;

using Protonox.Labs.Api;

using Tesseract;
using Protonox.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

using Serilog;
using Microsoft.Extensions.Logging;

namespace Protonox
{
    public sealed partial class App : Application
    {
        public App()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("log_.txt",
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true)
                .CreateLogger();

            Log.Logger.Information("[Startup]");
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.ClearProviders().AddSerilog();
            });
            services.AddSingleton(loggerFactory.CreateLogger(nameof(App)));

            services.AddSingleton<ElevenLabs>();

            services.AddSingleton<VoiceSettings>();

            services.AddSingleton<TesseractEngine>(x =>
                CreateTesseractEngine(x.GetRequiredService<TesseractCLIArgs>()));

            services.AddSingleton<AudioSettings>();

            services.AddSingleton<Controller>();
            services.AddSingleton<Window1>();
        }

        public static TesseractEngine CreateTesseractEngine(TesseractCLIArgs args)
        {
            Log.Logger.Information($"[Startup] Tesseract Path: `{args.Path}` | Lang: `{args.Lang}`");
            return new TesseractEngine(args.Path, args.Lang, EngineMode.Default);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ServiceCollection services = new();

            int errorCount = ProcessCLI(services, e.Args);
            if (errorCount > 0)
            {
                Current.Shutdown();
                return;
            }

            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();

            var mainWindow = serviceProvider.GetRequiredService<Window1>();
            mainWindow.Show();
        }

        private int ProcessCLI(ServiceCollection services, string[] args)
        {
            var apiKey = new Argument<string>("apikey", "ElevenLabs API key");

            var userAgent = new Option<string>(
                new[] { "--userAgent", "-ua" },
                () => { return null; }, "Either: 'rand' or UserAgent");

            var proxy = new Option<string>(
                new[] { "--proxy", "-p" },
                () => { return null; }, "Either: 'rand' or Proxy as format PROTOCOL://ADDRESS:PORT");

            var lang = new Option<string>(
                new[] { "--lang", "-l" },
                () => { return "eng"; }, "Tesseract Language model file name without extension.");

            var tessPath = new Option<string>(
                new[] { "--tpath", "-t" },
                () => { return @".\tessdata"; }, "Tesseract Language model path.");

            var rootCommand = new RootCommand() {
                apiKey,
                userAgent,
                proxy,
                lang,
                tessPath
            };

            rootCommand.SetHandler(
                ProcessArgs,
                apiKey,
                proxy,
                userAgent,
                lang,
                tessPath
            );

            var parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .Build();

            var result = parser.Parse(args);

            if (result.Errors.Count == 0)
                parser.Invoke(args);

            foreach (var error in result.Errors)
                Log.Logger.Error(error.Message);

            return result.Errors.Count;

            void ProcessArgs(string apiKey, string proxy, string userAgent, string lang, string tPath)
            {
                services.AddScoped((x) => new ElevenLabsCLIArgs
                {
                    ApiKey = apiKey,
                    Proxy = proxy,
                    UserAgent = userAgent
                });

                services.AddScoped((x) => new TesseractCLIArgs
                {
                    Lang = lang,
                    Path = tPath
                });
            }
        }
    }
}
