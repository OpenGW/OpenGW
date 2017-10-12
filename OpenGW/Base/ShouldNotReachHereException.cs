using System;
using System.Runtime.CompilerServices;

namespace OpenGW
{
    public class ShouldNotReachHereException : Exception
    {
        public ShouldNotReachHereException(
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = null)
            : base($"Should not reach here in '{memberName}': {filePath}:{lineNumber}")
        {
            // Do nothing
        }
    }
}
