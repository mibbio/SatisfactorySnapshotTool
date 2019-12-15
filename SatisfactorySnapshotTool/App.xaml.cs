using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace SatisfactorySnapshotTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            Current.DispatcherUnhandledException += DispatcherOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            var appRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var oldFiles = Directory.EnumerateFiles(appRoot).Where(f => f.EndsWith("old"));
            foreach (var file in oldFiles)
            {
                File.Delete(file);
            }
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            SendReport(e.Exception);
        }

        private void DispatcherOnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            SendReport(e.Exception);
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            SendReport((Exception)e.ExceptionObject);
        }

        public static void SendReport(Exception exception, string devMessage = "", bool silent = false)
        {
            var reportCrash = new CrashReporterDotNET.ReportCrash("resusseleman@gmail.com");
            reportCrash.DeveloperMessage = devMessage;
            reportCrash.IncludeScreenshot = true;
            reportCrash.Send(exception);
        }
    }
}
