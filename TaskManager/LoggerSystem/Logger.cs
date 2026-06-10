using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog.Sinks.File;
using Serilog.Sinks.SystemConsole; 
namespace TaskManager.LoggerSystem
{
    public class Logger
    {
        public static void Initialize()
        {
            // Если логгер уже создан 
            if (Log.Logger != null && Log.Logger.GetType().Name != "SilentLogger")
                return;

            const string logDirectory = "logs";
            const string logFileName = "app.log";
            const int maxFileSizeMb = 5;
            const int retainedFilesCount = 5;
            const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] [{SourceContext}] - {Message:lj}{NewLine}{Exception}";

            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logDirectory);
                if (!Directory.Exists(logPath))
                    Directory.CreateDirectory(logPath);

                var fullLogPath = Path.Combine(logPath, logFileName);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        path: fullLogPath,   
                        outputTemplate: outputTemplate,
                        fileSizeLimitBytes: maxFileSizeMb * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: retainedFilesCount
                    )
                    .CreateLogger();

                Log.Information("Логгер успешно инициализирован. Файл: {Path}", fullLogPath);
            }
            catch (Exception ex)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console(outputTemplate: outputTemplate)
                    .CreateLogger();

                Log.Error(ex, "Критическая ошибка при инициализации файлового логгера. Используется консольный логгер.");
            }
        }
    }
}
