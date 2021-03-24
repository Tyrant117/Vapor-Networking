
namespace VaporNetworking
{
    /// <summary>
    /// Simple logger to show level of concern for the network.
    /// </summary>
    public class NetLogFilter
    {
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warn = 2,
            Error = 3,
            Fatal = 4,
        };

        public static bool spew;
        public static bool messageDiagnostics;

        public const int Debug = 0;
        public const int Info = 1;
        public const int Warn = 2;
        public const int Error = 3;
        public const int Fatal = 4;

        private static int currentLogLevel = Info;
        public static int CurrentLogLevel { get { return currentLogLevel; } set { currentLogLevel = value; } }

        public static bool logDebug { get { return currentLogLevel <= Debug; } }
        public static bool logInfo { get { return currentLogLevel <= Info; } }
        public static bool logWarn { get { return currentLogLevel <= Warn; } }
        public static bool logError { get { return currentLogLevel <= Error; } }
        public static bool logFatal { get { return currentLogLevel <= Fatal; } }
    }
}