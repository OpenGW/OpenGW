using System;

namespace OpenGW
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            ByteBuffer bb = new ByteBuffer(1);

            bb.Append(new byte[1024 * 1024], 0, 65536 * 3 - 1);
            Console.WriteLine(bb);

            bb.Start = 4;
            Console.WriteLine(bb);

            ++bb.Start;
            Console.WriteLine(bb);

            --bb.Length;
            Console.WriteLine(bb);
        }
    }
}
