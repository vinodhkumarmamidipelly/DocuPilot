using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Extension methods to make migration from Console.WriteLine easier
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Log information message (replaces Console.WriteLine for info messages)
        /// </summary>
        public static void LogInfo(this ILogger logger, string message, params object[] args)
        {
            logger.LogInformation(message, args);
        }

        /// <summary>
        /// Log warning message (replaces Console.WriteLine for warnings)
        /// </summary>
        public static void LogWarn(this ILogger logger, string message, params object[] args)
        {
            logger.LogWarning(message, args);
        }

        /// <summary>
        /// Log error message (replaces Console.WriteLine for errors)
        /// </summary>
        public static void LogErr(this ILogger logger, string message, params object[] args)
        {
            logger.LogError(message, args);
        }

        /// <summary>
        /// Log error with exception
        /// </summary>
        public static void LogErr(this ILogger logger, Exception ex, string message, params object[] args)
        {
            logger.LogError(ex, message, args);
        }
    }
}

