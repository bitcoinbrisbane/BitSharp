using Ninject;
using Ninject.Modules;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node
{
    public class LoggingModule : NinjectModule
    {
        private readonly string directory;
        private readonly LogLevel logLevel;

        public LoggingModule(string baseDirectory, LogLevel logLevel)
        {
            this.directory = baseDirectory;
            this.logLevel = logLevel;
        }

        public override void Load()
        {
            // initialize logging configuration
            var config = new LoggingConfiguration();

            // shared layout
            var layout = "${message} ${exception:separator=\r\n:format=message,type,method,stackTrace}";

            // create console target
            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            // console settings
            consoleTarget.Layout = layout;

            // console rules
            config.LoggingRules.Add(new LoggingRule("*", logLevel, consoleTarget));

            // create file target
            var fileTarget = new FileTarget() { AutoFlush = false };
            config.AddTarget("file", fileTarget);

            // file settings
            fileTarget.FileName = Path.Combine(this.directory, "BitSharp.log");
            
            fileTarget.Layout = layout;
            fileTarget.DeleteOldFileOnStartup = true;

            // file rules
            config.LoggingRules.Add(new LoggingRule("*", logLevel, fileTarget));

            // activate configuration and bind
            LogManager.Configuration = config;
            this.Kernel.Bind<Logger>().ToMethod(context => LogManager.GetLogger("BitSharp"));
        }
    }
}
