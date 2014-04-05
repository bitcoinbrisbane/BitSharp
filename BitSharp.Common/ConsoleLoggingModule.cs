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

namespace BitSharp.Common
{
    public class ConsoleLoggingModule : NinjectModule
    {
        private readonly LogLevel logLevel;

        public ConsoleLoggingModule(LogLevel logLevel = null)
        {
            this.logLevel = logLevel ?? LogLevel.Debug;
        }

        public override void Load()
        {
            // initialize logging configuration
            var config = new LoggingConfiguration();

            // create console target
            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            // console settings
            consoleTarget.Layout = "${message} ${exception:separator=\r\n:format=message,type,method,stackTrace}";

            // console rules
            config.LoggingRules.Add(new LoggingRule("*", this.logLevel, consoleTarget));

            // activate configuration and bind
            LogManager.Configuration = config;
            this.Kernel.Bind<Logger>().ToMethod(context => LogManager.GetLogger("BitSharp"));
        }
    }
}
