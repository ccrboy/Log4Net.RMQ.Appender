using log4net.Appender;
using log4net.Core;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Log4Net.Appenders.RabbitMQ
{
    public class RabbitMQAppender : AppenderSkeleton
    {
        #region Fields

        //"amqp://guest:guest@localhost:5672";

        public RabbitMQAppender()
        {
            Login = "guest";
            Password = "guest";
            Host = "localhost";
            Protocol = "amqp";
            Port = "5672";

            EmitStackTraceWhenAvailable = false;
        }

        public string ExchangeName { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public string Protocol { get; set; }
        public string Port { get; set; }
        public string ApiName { get; set; }

        private Regex filterList = null;

        public bool EmitStackTraceWhenAvailable { get; set; }

        public bool IncludeAllProperties { get; set; }

        #endregion

        #region Properties

        private IModel? RabbitMQChannel { get; set; }

        private string Uri()
        {
            var uri = $"{Login}:{Password}@{Host}:{Port}";
            if (string.IsNullOrWhiteSpace(Protocol))
            {
                return uri;
            }

            return $"{Protocol}://{uri}";
        }

        #endregion

        #region Inherited

        protected override void Append(LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderLoggingEvent(loggingEvent);

            var record = new Dictionary<string, object> {
                { "level", loggingEvent.Level.Name },
                { "message", renderedMessage },
                { "logger_name", loggingEvent.LoggerName }
            };

            if (EmitStackTraceWhenAvailable && !string.IsNullOrWhiteSpace(loggingEvent.ExceptionObject?.StackTrace))
            {
                var transcodedFrames = new List<Dictionary<string, object>>();
                var stackTrace = new StackTrace(true);
                foreach (StackFrame frame in stackTrace.GetFrames())
                {
                    var transcodedFrame = new Dictionary<string, object>
                    {
                        { "filename", frame.GetFileName() },
                        { "line", frame.GetFileLineNumber() },
                        { "column", frame.GetFileColumnNumber() },
                        { "method", frame.GetMethod().ToString() },
                        { "il_offset", frame.GetILOffset() },
                        { "native_offset", frame.GetNativeOffset() },
                    };
                    transcodedFrames.Add(transcodedFrame);
                }
                record.Add("stacktrace", transcodedFrames);
            }

            loggingEvent.GetLoggingEventData();

            if (IncludeAllProperties && loggingEvent.Properties.Count > 0)
            {
                foreach (var key in loggingEvent.Properties.GetKeys())
                {
                    var val = loggingEvent.Properties[key];
                    if (val == null)
                        continue;

                    if (filterList == null || filterList.IsMatch(key))
                    {
                        record.Add(key, SerializePropertyValue(key, val));
                    }
                }
            }

            try
            {
                EnsureConnected();
            }
            catch (Exception ex)
            {
                base.ErrorHandler.Error($"{nameof(RabbitMQAppender)} EnsureConnected - {ex.Message}");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(ApiName))
                {
                    Console.WriteLine("ApiName was empty, fill with value [Default_ApiName]");
                    ApiName = "Default_ApiName";
                }

                record.Add("ApiName", ApiName);

                Console.WriteLine(JsonConvert.SerializeObject(loggingEvent));
                
                var message = JsonConvert.SerializeObject(record);
                var body = Encoding.UTF8.GetBytes(message);

                if (string.IsNullOrWhiteSpace(ExchangeName))
                {
                    Console.WriteLine("ExchangeName was empty, fill with value [Default_ExchangeName]");
                    ExchangeName = "Default_ExchangeName";
                }
                
                var routingKey = nameof(String);

                RabbitMQChannel.BasicPublish(exchange: ExchangeName, routingKey: routingKey, basicProperties: null, body: body);
            }
            catch (Exception ex)
            {
                base.ErrorHandler.Error($"{nameof(RabbitMQAppender)} BasicPublish - {ex.Message}");
            }
        }

        #endregion

        #region Methods

        private void EnsureConnected()
        {
            if (RabbitMQChannel == null)
            {
                InitializeClient();
            }
        }

        private void InitializeClient()
        {
            var factory = new ConnectionFactory { Uri = new Uri(Uri()) };
            var connection = factory.CreateConnection();
            RabbitMQChannel = connection.CreateModel();
        }

        private static object SerializePropertyValue(string propertyKey, object propertyValue)
        {
            if (propertyValue == null || Convert.GetTypeCode(propertyValue) != TypeCode.Object || propertyValue is decimal)
            {
                return propertyValue;   // immutable
            }
            else
            {
                return propertyValue.ToString();
            }
        }

        #endregion
    }
}