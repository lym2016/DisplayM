using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using Visualizer.Core.Defaults.Visualizer.Core.Services.Impl;
using Visualizer.Core.Defaults.Visualizer.Core.Services;
using Visualizer.ViewModels;

namespace DisplayM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // 在应用启动时配置服务
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure =>
            {
              //  configure.AddConsole();
             //   configure.AddDebug();
            });

            // 注册其他服务
            services.AddSingleton<IConfigurationService, ConfigurationService>();
          //  services.AddSingleton<IValueFormatterFactory, ValueFormatterFactory>();
            // ...
        }
    }

}
