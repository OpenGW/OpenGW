using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.CompilerServices;

namespace OpenGW
{
    internal static class Extensions
    {
        public static string[] SplitToLines(this string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        
        private class ObjectReferenceEqualityComparer : IEqualityComparer<(object, string, int)>
        {
            bool IEqualityComparer<(object, string, int)>.Equals((object, string, int) x, (object, string, int) y)
            {
                return object.ReferenceEquals(x.Item1, y.Item1) &&
                       x.Item2 == y.Item2 &&
                       x.Item3 == y.Item3;
            }

            int IEqualityComparer<(object, string, int)>.GetHashCode((object, string, int) obj)
            {
                return (object.ReferenceEquals(obj.Item1, null) ? 0 : obj.Item1.GetHashCode()) ^
                       (obj.Item2.GetHashCode()) ^
                       (obj.Item3.GetHashCode());
            }
        }
        
        private static readonly ConcurrentDictionary<(object, string, int), bool> s_Reached = 
            new ConcurrentDictionary<(object, string, int), bool>(new ObjectReferenceEqualityComparer()); 
        
        [Conditional("DEBUG")]
        public static void ShouldReachHereOnce(
            this object instance,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!s_Reached.TryAdd((instance, filePath, lineNumber), true))
            {
                throw new Exception($"Should not reach here more than once! {filePath}:{lineNumber}");
            }
        }
    }
}
