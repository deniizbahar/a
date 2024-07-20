using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Günlük;
using MainForm;
using Serilog.Events;

namespace ControlService
{
    public class MyService : ServiceBase
    {
        private Task _executingTask;
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private ServiceConfigDto _mywebapipoolConfig = new ServiceConfigDto { Application = ApplicationType.mywebapipool, LogLevel = LogLevel.Information, MonitoringFrequency = 10 };
        private ServiceConfigDto _mockServiceConfig = new ServiceConfigDto { Application = ApplicationType.MockService, LogLevel = LogLevel.Information, MonitoringFrequency = 10 };
        private ServiceConfigDto _controlServiceConfig = new ServiceConfigDto { Application = ApplicationType.ControlService, LogLevel = LogLevel.Information };

        public MyService()
        {
            this.ServiceName = "ControlService";
        }

        protected override void OnStart(string[] args)
        {
            var initialLogLevel = LogLevelConverter.ToSerilogLevel(_controlServiceConfig.LogLevel);
            LoggerHelper.ConfigureLogger(new LoggerService("C://Logs/monitoring-log.json", initialLogLevel));
            LoggerHelper.Log(LogEventLevel.Information, "Control Service started.");

            SubscribeToConfigChanges();

            _executingTask = ExecuteAsync(_stoppingCts.Token);
        }

        protected override void OnStop()
        {
            if (_executingTask == null)
            {
                return;
            }

            _stoppingCts.Cancel();
            _executingTask.Wait();
            LoggerHelper.Log(LogEventLevel.Information, "Control Service stopped.");
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    LoggerHelper.Log(LogEventLevel.Information, "Worker running");

                    if (_mockServiceConfig != null && _mockServiceConfig.MonitoringFrequency.HasValue)
                    {
                        await CheckAndRestartServiceAsync("MockService", _mockServiceConfig.MonitoringFrequency.Value, stoppingToken);
                    }

                    if (_mywebapipoolConfig != null && _mywebapipoolConfig.MonitoringFrequency.HasValue)
                    {
                        await CheckAndRestartAppPoolAsync("mywebapipool", _mywebapipoolConfig.MonitoringFrequency.Value, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    LoggerHelper.Log(LogEventLevel.Error, $"An error occurred: {ex.Message}");
                }

                await Task.Delay(1000); // Ana döngüde bekleme süresi (1 saniye) ekleyin
            }
        }

        private async Task CheckAndRestartServiceAsync(string serviceName, int frequency, CancellationToken stoppingToken)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
                    {
                        LoggerHelper.Log(LogEventLevel.Warning, $"Service {serviceName} is stopped. Attempting to restart.");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1));
                        LoggerHelper.Log(LogEventLevel.Information, $"Service {serviceName} restarted successfully.");
                    }
                    else
                    {
                        LoggerHelper.Log(LogEventLevel.Information, $"Service {serviceName} is running.");
                    }
                }
                await Task.Delay(frequency * 1000, stoppingToken); // Belirtilen aralıklarla kontrol
            }
            catch (Exception ex)
            {
                LoggerHelper.Log(LogEventLevel.Error, $"Error checking or restarting service {serviceName}: {ex.Message}");
            }
        }

        private async Task CheckAndRestartAppPoolAsync(string appPoolName, int frequency, CancellationToken stoppingToken)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Import-Module WebAdministration; $appPool = Get-Item 'IIS:\\AppPools\\{appPoolName}'; if ($appPool.state -eq 'Stopped') {{ Start-WebAppPool -Name '{appPoolName}'; Write-Output 'App Pool {appPoolName} restarted successfully.'; }} else {{ Write-Output 'App Pool {appPoolName} is running.'; }}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(output))
                    {
                        LoggerHelper.Log(LogEventLevel.Information, output);
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        LoggerHelper.Log(LogEventLevel.Error, $"Error restarting App Pool {appPoolName}: {error}");
                    }
                }
                await Task.Delay(frequency * 1000, stoppingToken); // Belirtilen aralıklarla kontrol
            }
            catch (Exception ex)
            {
                LoggerHelper.Log(LogEventLevel.Error, $"Error checking or restarting App Pool {appPoolName}: {ex.Message}");
            }
        }

        private void SubscribeToConfigChanges()
        {
            Form2.ConfigChanged += Form2_ConfigChanged;
            LoggerHelper.Log(LogEventLevel.Information, "Subscribed to Form2 ConfigChanged event.");
        }

        private void Form2_ConfigChanged(object sender, ConfigChangedEventArgs e)
        {
            LoggerHelper.Log(LogEventLevel.Information, $"ConfigChanged event received: {e.NewConfig.Application}, LogLevel: {e.NewConfig.LogLevel}, MonitoringFrequency: {e.NewConfig.MonitoringFrequency}");

            if (e.NewConfig.Application == ApplicationType.ControlService)
            {
                _controlServiceConfig = e.NewConfig;
                var newLogLevel = LogLevelConverter.ToSerilogLevel(_controlServiceConfig.LogLevel);
                LoggerHelper.SetLogLevel(newLogLevel);
                LoggerHelper.Log(LogEventLevel.Information, $"Updated ControlService config: LogLevel = {_controlServiceConfig.LogLevel}");
            }
            else if (e.NewConfig.Application == ApplicationType.MockService)
            {
                _mockServiceConfig = e.NewConfig;
                LoggerHelper.Log(LogEventLevel.Information, $"Updated MockService config: Frequency = {_mockServiceConfig.MonitoringFrequency}");
            }
            else if (e.NewConfig.Application == ApplicationType.mywebapipool)
            {
                _mywebapipoolConfig = e.NewConfig;
                LoggerHelper.Log(LogEventLevel.Information, $"Updated mywebapipool config: Frequency = {_mywebapipoolConfig.MonitoringFrequency}");
            }
        }
    }
}
