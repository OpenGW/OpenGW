using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenGW
{
    internal enum LogLevel
    {
        Dummy = 0,
        Trace = 1,
        Debug,
        Info,
        Warn,
        Error,
        Fatal,
    }

    internal static class Logger
    {
        public static LogLevel MinLogLevel { get; set; } = LogLevel.Trace;
        public static LogLevel MaxLogLevel { get; set; } = LogLevel.Fatal;


        private static readonly ConcurrentDictionary<(string FilePath, int LineNumber), string> s_CallerPrefixes 
            = new ConcurrentDictionary<(string, int), string>();

        private static readonly int s_LogLevelMaxLength = 0;

        static Logger()
        {
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>()) {
                s_LogLevelMaxLength = Math.Max(s_LogLevelMaxLength, level.ToString().Length);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Log(
            LogLevel level, string message, Exception exception, 
            string filePath, int lineNumber, string memberName)
        {
            if (level < Logger.MinLogLevel || level > Logger.MaxLogLevel) {
                return;
            }

            string timeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | ";

            if (!s_CallerPrefixes.TryGetValue((filePath, lineNumber), out string callerPrefix)) {
                filePath = filePath.Substring(filePath.LastIndexOfAny(new[] {'/', '\\'}) + 1);
                string levelString = level.ToString().ToUpper().PadRight(s_LogLevelMaxLength);
                string callerString = $"{filePath}:{lineNumber}".PadRight(24) + $" | [{memberName}] ";
                callerPrefix = $"{levelString} | {callerString}";
                s_CallerPrefixes.TryAdd((filePath, lineNumber), callerString);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(timeNow);
            if (message.IndexOfAny(new[] {'\r', '\n'}) >= 0) {  // message is multiline

                string[] lines = message.SplitToLines();  // lines.Length > 1
                sb.Append(callerPrefix);
                sb.Append(lines[0]);
                sb.Append(Environment.NewLine);
                string nextPrefix = new string(' ', callerPrefix.Length + timeNow.Length) + "  ";
                for (int i = 1; i < lines.Length; i++) {
                    sb.Append(nextPrefix);
                    sb.Append(lines[i]);
                    sb.Append(Environment.NewLine);
                }
            }
            else {
                sb.Append(callerPrefix);
                sb.Append(message);
                sb.Append(Environment.NewLine);
            }

            if (exception != null) {
                sb.Append(new string(' ', 4));
                sb.Append("--------------------------------");
                sb.Append(Environment.NewLine);
                string nextPrefix = new string(' ', 4);
                string[] lines = exception.ToString().SplitToLines();
                foreach (string line in lines) {
                    sb.Append(nextPrefix);
                    sb.Append(line);
                    sb.Append(Environment.NewLine);
                }
                sb.Append(Environment.NewLine);
            }

            Console.Write(sb.ToString());
        }


        #region Trace

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Trace(
            string message,
            Exception exception = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Trace, message, exception, filePath, lineNumber, memberName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Trace(
            Exception exception,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Trace, $"{exception.GetType()}:", exception, filePath, lineNumber, memberName);
        }

        #endregion

        #region Debug

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Debug(
            string message,
            Exception exception = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Debug, message, exception, filePath, lineNumber, memberName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Debug(
            Exception exception,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Debug, $"{exception.GetType()}:", exception, filePath, lineNumber, memberName);
        }

        #endregion

        #region Info

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Info(
            string message,
            Exception exception = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Info, message, exception, filePath, lineNumber, memberName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Info(
            Exception exception,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Info, $"{exception.GetType()}:", exception, filePath, lineNumber, memberName);
        }

        #endregion

        #region Warn

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Warn(
            string message,
            Exception exception = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Warn, message, exception, filePath, lineNumber, memberName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Warn(
            Exception exception,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Warn, $"{exception.GetType()}:", exception, filePath, lineNumber, memberName);
        }

        #endregion

        #region Error

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Error(
            string message,
            Exception exception = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Error, message, exception, filePath, lineNumber, memberName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Error(
            Exception exception,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Error, $"{exception.GetType()}:", exception, filePath, lineNumber, memberName);
        }

        #endregion

        #region Fatal

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Fatal(
            string message,
            Exception exception = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Fatal, message, exception, filePath, lineNumber, memberName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Fatal(
            Exception exception,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
        {
            Logger.Log(LogLevel.Fatal, $"{exception.GetType()}:", exception, filePath, lineNumber, memberName);
        }

        #endregion

    }
}
