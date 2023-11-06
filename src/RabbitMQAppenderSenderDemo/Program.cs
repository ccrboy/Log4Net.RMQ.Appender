using System.Reflection;
using log4net;
using log4net.Config;

namespace RabbitMQAppenderSenderDemo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            _log.Error("Error Message", new Exception("This is Exception"));

            Console.WriteLine("Press ENTER key");
            Console.ReadKey();
        }
    }
}