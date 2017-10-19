using System;

namespace OpenGW
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            ByteBuffer bb = new ByteBuffer();

            bb.Append(new byte[4] {1, 2, 3, 4}, 0, 4);
            Console.WriteLine(bb);

            bb.Start = 4;
            Console.WriteLine(bb);

            --bb.Start;
            Console.WriteLine(bb);

            ++bb.Length;
            Console.WriteLine(bb);
        }
    }
}
