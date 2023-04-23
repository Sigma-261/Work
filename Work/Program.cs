using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Work;

class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
           .ConfigureServices((hostContext, services) =>
           {
               var separateConfig = new ConfigurationBuilder()
                .AddJsonFile("config.json", optional: false, reloadOnChange: true)
                .Build();

               services.AddCronJob<ConsoleHostedService>(c =>
               {
                   c.TimeZoneInfo = TimeZoneInfo.Local;
                   c.CronExpression = @"* 8-18 * * *";
               })
               .Configure<Config>(separateConfig);
           })
           .RunConsoleAsync();
    }
}

internal sealed class ConsoleHostedService : CronJobService
{
    private readonly ILogger _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    private Config _options;

    private List<string> _created;
    private List<string> _edited;
    private List<string> _deleted;

    public ConsoleHostedService(IScheduleConfig<ConsoleHostedService> config,
         IHostApplicationLifetime appLifetime,
        ILogger<ConsoleHostedService> logger,
        IOptionsMonitor<Config> options)
        : base(config.CronExpression, config.TimeZoneInfo)
    {
        _logger = logger;
        _appLifetime = appLifetime;

        options.OnChange(x => {
            _options = x;
        });
        _options = options.CurrentValue;

        _created = new List<string>();
        _edited = new List<string>();
        _deleted = new List<string>();
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("App launched");
        return base.StartAsync(cancellationToken);
    }

    public override Task DoWork(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {

                FileSystemWatcher fileSystemWatcher = new FileSystemWatcher();
                if (!Directory.Exists(_options.Path))
                {
                    _logger.LogInformation("{0} Don`t exist!", _options.Path);
                }
                fileSystemWatcher.Path = _options.Path;
                fileSystemWatcher.IncludeSubdirectories = true;

                fileSystemWatcher.Created += FileSystemWatcherEvent_Created;
                fileSystemWatcher.Renamed += FileSystemWatcherEvent_Renamed;
                fileSystemWatcher.Deleted += FileSystemWatcherEvent_Deleted;
                fileSystemWatcher.EnableRaisingEvents = true;

                void FileSystemWatcherEvent_Created(object sender, FileSystemEventArgs e)
                {
                    string value = $"Create: {e.FullPath}";
                    _created.Add(e.FullPath);
                    _logger.LogInformation(value);
                }

                void FileSystemWatcherEvent_Renamed(object sender, FileSystemEventArgs e)
                {
                    string value = $"Edit: {e.FullPath}";
                    _edited.Add(e.FullPath);
                    _logger.LogInformation(value);
                }

                void FileSystemWatcherEvent_Deleted(object sender, FileSystemEventArgs e)
                {
                    string value = $"Delete: {e.FullPath}";
                    _deleted.Add(e.FullPath);
                    _logger.LogInformation(value);
                }
            });
        });
        

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Created:");
        _created.ForEach(i => Console.Write("{0}\n", i));
        Console.WriteLine("\nEdited:");
        _edited.ForEach(i => Console.Write("{0}\n", i));
        Console.WriteLine("\nDeleted:");
        _deleted.ForEach(i => Console.Write("{0}\n", i));
        _logger.LogInformation("App stopped");

        return base.StopAsync(cancellationToken);
    }
}