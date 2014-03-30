using Ninject;
using Ninject.Modules;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node
{
    public class LoggingModule : NinjectModule
    {
        private readonly LogLevel logLevel;

        public LoggingModule(LogLevel logLevel)
        {
            this.logLevel = logLevel;
        }

        public override void Load()
        {
            // Step 1. Create configuration object 
            var config = new LoggingConfiguration();

            // Step 2. Create targets and add them to the configuration 
            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            var fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties 
            consoleTarget.Layout = @"${date:format=HH\\:MM\\:ss} ${logger} ${message}";
            fileTarget.FileName = "${basedir}/BitSharp.log";
            fileTarget.Layout = "${message}";
            fileTarget.DeleteOldFileOnStartup = true;

            // Step 4. Define rules
            var rule1 = new LoggingRule("*", logLevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            var rule2 = new LoggingRule("*", logLevel, fileTarget);
            config.LoggingRules.Add(rule2);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;

            this.Kernel.Bind<Logger>().ToMethod(context => LogManager.GetLogger("BitSharp"));
        }
    }
}
