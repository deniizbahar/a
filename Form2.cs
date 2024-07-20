using System;
using System.ServiceProcess;
using System.Windows.Forms;
using Günlük;
using Microsoft.Web.Administration;
using Serilog.Events;

namespace MainForm
{
    public partial class Form2 : Form
    {
        public static event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        private string controlServiceName = "ControlService";
        private string mockServiceName = "MockService";
        private string appPoolName = "mywebapipool";

        public Form2()
        {
            InitializeComponent();
            InitializeComboBoxes();
            LoggerHelper.ConfigureLogger(new LoggerService("C://Logs/form-config-changes-log.json", LogEventLevel.Information));
        }

        private void InitializeComboBoxes()
        {
            cbControlServiceLogLevel.Items.AddRange(Enum.GetNames(typeof(LogLevel)));
            cbWebApiLogLevel.Items.AddRange(Enum.GetNames(typeof(LogLevel)));
            cbMockServiceLogLevel.Items.AddRange(Enum.GetNames(typeof(LogLevel)));

            // Varsayılan değerleri seçin
            cbControlServiceLogLevel.SelectedIndex = 2; // Information
            cbWebApiLogLevel.SelectedIndex = 2; // Information
            cbMockServiceLogLevel.SelectedIndex = 2; // Information
        }

        private void btnStartControlService_Click(object sender, EventArgs e)
        {
            StartService(controlServiceName, lblControlServiceStatus);
            var config = new ServiceConfigDto { Application = ApplicationType.ControlService, LogLevel = GetSelectedLogLevel(cbControlServiceLogLevel) };
            OnConfigChanged(config);
        }

        private void btnStopControlService_Click(object sender, EventArgs e)
        {
            StopService(controlServiceName, lblControlServiceStatus);
        }

        private void btnStartMockService_Click(object sender, EventArgs e)
        {
            StartService(mockServiceName, lblMockServiceStatus);
            var config = new ServiceConfigDto { Application = ApplicationType.MockService, LogLevel = GetSelectedLogLevel(cbMockServiceLogLevel), MonitoringFrequency = int.Parse(txtMockServiceFrequency.Text) };
            OnConfigChanged(config);
        }

        private void btnStopMockService_Click(object sender, EventArgs e)
        {
            StopService(mockServiceName, lblMockServiceStatus);
        }

        private void btnStartAppPool_Click(object sender, EventArgs e)
        {
            StartAppPool(appPoolName, lblAppPoolStatusIndicator);
            var config = new ServiceConfigDto { Application = ApplicationType.mywebapipool, LogLevel = GetSelectedLogLevel(cbWebApiLogLevel), MonitoringFrequency = int.Parse(txtWebApiFrequency.Text) };
            OnConfigChanged(config);
        }

        private void btnStopAppPool_Click(object sender, EventArgs e)
        {
            StopAppPool(appPoolName, lblAppPoolStatusIndicator);
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            try
            {
                OnConfigChanged(new ServiceConfigDto
                {
                    Application = ApplicationType.ControlService,
                    LogLevel = GetSelectedLogLevel(cbControlServiceLogLevel)
                });

                OnConfigChanged(new ServiceConfigDto
                {
                    Application = ApplicationType.mywebapipool,
                    LogLevel = GetSelectedLogLevel(cbWebApiLogLevel),
                    MonitoringFrequency = int.Parse(txtWebApiFrequency.Text)
                });

                OnConfigChanged(new ServiceConfigDto
                {
                    Application = ApplicationType.MockService,
                    LogLevel = GetSelectedLogLevel(cbMockServiceLogLevel),
                    MonitoringFrequency = int.Parse(txtMockServiceFrequency.Text)
                });

                MessageBox.Show("Ayarlar kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar kaydedilirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartService(string serviceName, Label statusLabel)
        {
            try
            {
                ServiceController service = new ServiceController(serviceName);
                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running);
                    statusLabel.Text = "Başlatıldı";
                }
                else
                {
                    statusLabel.Text = "Zaten çalışıyor";
                }
            }
            catch (InvalidOperationException ex)
            {
                statusLabel.Text = $"Hata: {ex.Message}";
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                statusLabel.Text = $"Yönetici hakları gereklidir: {ex.Message}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Hata: {ex.Message}";
            }
        }

        private void StopService(string serviceName, Label statusLabel)
        {
            try
            {
                ServiceController service = new ServiceController(serviceName);
                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped);
                    statusLabel.Text = "Durduruldu";
                }
                else
                {
                    statusLabel.Text = "Zaten durdurulmuş";
                }
            }
            catch (InvalidOperationException ex)
            {
                statusLabel.Text = $"Hata: {ex.Message}";
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                statusLabel.Text = $"Yönetici hakları gereklidir: {ex.Message}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Hata: {ex.Message}";
            }
        }

        private void StartAppPool(string appPoolName, Label statusLabel)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ApplicationPool appPool = serverManager.ApplicationPools[appPoolName];
                    if (appPool != null && appPool.State == ObjectState.Stopped)
                    {
                        appPool.Start();
                        statusLabel.Text = "Başlatıldı";
                    }
                    else
                    {
                        statusLabel.Text = "Zaten çalışıyor";
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Hata: {ex.Message}";
            }
        }

        private void StopAppPool(string appPoolName, Label statusLabel)
        {
            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ApplicationPool appPool = serverManager.ApplicationPools[appPoolName];
                    if (appPool != null && appPool.State == ObjectState.Started)
                    {
                        appPool.Stop();
                        statusLabel.Text = "Durduruldu";
                    }
                    else
                    {
                        statusLabel.Text = "Zaten durdurulmuş";
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Hata: {ex.Message}";
            }
        }

       

        protected virtual void OnConfigChanged(ServiceConfigDto newConfig)
        {
            if (ConfigChanged != null)
            {
                // Dinleyici varsa
                LoggerHelper.ConfigureLogger(new LoggerService("C://Logs/event_subscribed_log.json", LogEventLevel.Information));
                LoggerHelper.Log(LogEventLevel.Information, $"ConfigChanged event triggered with new config: {newConfig.Application}, LogLevel: {newConfig.LogLevel}, MonitoringFrequency: {newConfig.MonitoringFrequency}");
                ConfigChanged.Invoke(this, new ConfigChangedEventArgs { NewConfig = newConfig });
            }
            else
            {
                // Dinleyici yoksa
                LoggerHelper.ConfigureLogger(new LoggerService("C://Logs/no_event_subscribed_log.json", LogEventLevel.Information));
                LoggerHelper.Log(LogEventLevel.Information, $"No subscribers for ConfigChanged event. New config: {newConfig.Application}, LogLevel: {newConfig.LogLevel}, MonitoringFrequency: {newConfig.MonitoringFrequency}");
            }
        }

        private LogLevel GetSelectedLogLevel(ComboBox comboBox)
        {
            return (LogLevel)Enum.Parse(typeof(LogLevel), comboBox.SelectedItem.ToString());
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            InitializeComboBoxes();
        }
    }
}
