using System;
using System.Windows;
using BackupApp.Core.Repositories;
using BackupApp.Core.Services;
using BackupApp.Data;
using BackupApp.Data.Repositories;
using BackupApp.WPF.Services;
using BackupApp.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BackupApp.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IHost? HostInstance { get; private set; }
    private IServiceScope? _appScope;

protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HostInstance = CreateHostBuilder().Build();
        HostInstance.Start();

        _appScope = HostInstance.Services.CreateScope();
        var services = _appScope.ServiceProvider;

        // Применяем миграции при старте, чтобы гарантированно были созданы таблицы.
        var dbContext = services.GetRequiredService<BackupAppDbContext>();
        dbContext.Database.Migrate();

        var mainWindow = services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (HostInstance is not null)
       {
            var scheduler = HostInstance.Services.GetService<ISchedulerService>();
            scheduler?.StopScheduler();

            _appScope?.Dispose();
            _appScope = null;

            await HostInstance.StopAsync();
            HostInstance.Dispose();
        }

        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var connectionString = "Data Source=backupapp.db";

                services.AddDbContext<BackupAppDbContext>(options =>
                    options.UseSqlite(connectionString));

                // Репозитории
                services.AddScoped<IBackupTaskRepository, BackupTaskRepository>();

                // Сервисы
                services.AddSingleton<IBackupService, BackupService>();
                services.AddSingleton<IEncryptionService, EncryptionService>();
                services.AddSingleton<IRetentionService, RetentionService>();
                services.AddSingleton<ISchedulerService, SchedulerService>();
               services.AddSingleton<IMessageService, MessageService>();
                services.AddSingleton<IPathDialogService, PathDialogService>();

                // ViewModels
                services.AddTransient<DecryptWindowViewModel>(sp =>
                    new DecryptWindowViewModel(
                        sp.GetRequiredService<IBackupService>(),
                        sp.GetRequiredService<IMessageService>(),
                        sp.GetRequiredService<IPathDialogService>()));
                services.AddTransient<Func<DecryptWindowViewModel>>(sp => () => sp.GetRequiredService<DecryptWindowViewModel>());
                services.AddTransient<TaskViewModel>(sp =>
                    new TaskViewModel(
                        sp.GetRequiredService<IPathDialogService>(),
                        sp.GetRequiredService<IEncryptionService>()));
                services.AddTransient<Func<TaskViewModel>>(sp => () => sp.GetRequiredService<TaskViewModel>());
                services.AddScoped<MainWindowViewModel>(sp => 
                    new MainWindowViewModel(
                        sp.GetRequiredService<IBackupTaskRepository>(),
                        sp.GetRequiredService<IBackupService>(),
                        sp.GetRequiredService<ISchedulerService>(),
                        sp.GetRequiredService<IRetentionService>(),
                        sp.GetRequiredService<IMessageService>(),
                        sp.GetRequiredService<Func<TaskViewModel>>(),
                        sp.GetRequiredService<Func<DecryptWindowViewModel>>()));

                // Views
                services.AddTransient<MainWindow>();
            });
    }
}

