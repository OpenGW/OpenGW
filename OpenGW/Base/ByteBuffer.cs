using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenGW
{
    public class ByteBuffer : IReusable, IEnumerable<byte>
    {
        private const int MAX_RESISE_INCREMENT = 1024 * 64;  // 64 KB

        private static int CalcNewMaxLength(int currMaxLength, int minRequired)
        {
            Debug.Assert(currMaxLength >= 0);
            Debug.Assert(minRequired > currMaxLength);

            if (minRequired > MAX_RESISE_INCREMENT) {
                return (minRequired + MAX_RESISE_INCREMENT - 1) / MAX_RESISE_INCREMENT * MAX_RESISE_INCREMENT;
            }
            else {
                return currMaxLength * 2;
            }
        }

        
        private byte[] m_Array;
        private int m_Start;
        private int m_Length;

        public byte[] Array => this.m_Array;

        public int Start {
            get => this.m_Start;
            set {
                Debug.Assert(value >= 0 && value <= this.m_Start + this.m_Length);

                this.m_Length += this.m_Start - value;
                this.m_Start = value;
            }
        }

        public int MaxLength => this.m_Array.Length - this.m_Start;

        public int Capacity => this.m_Array.Length;

        public int Length {
            get => this.m_Length;
            set {
                Debug.Assert(value >= 0 && value < this.MaxLength);
                this.m_Length = value;
            }
        }


        public byte this[int index] {
            get => this.m_Array[this.m_Start + index];
            set => this.m_Array[this.m_Start + index] = value;
        }


        public static explicit operator ArraySegment<byte>(ByteBuffer byteBuffer)
        {
            return new ArraySegment<byte>(byteBuffer.m_Array, byteBuffer.m_Start, byteBuffer.m_Length);
        }


        public void Append(byte[] buffer, int start, int count)
        {
            Debug.Assert(start >= 0 && start + count <= buffer.Length);

            int minRequired = this.m_Start + this.m_Length + count;
            if (minRequired > this.m_Array.Length) {
                System.Array.Resize(ref this.m_Array, CalcNewMaxLength(this.MaxLength, minRequired));
            }

            System.Array.Copy(buffer, start, this.m_Array, this.m_Start + this.m_Length, count);
            this.m_Length += count;
        }

        public void Append(byte[] buffer)
        {
            this.Append(buffer, 0, buffer.Length);
        }

        public void Append(ArraySegment<byte> segBytes)
        {
            this.Append(segBytes.Array, segBytes.Offset, segBytes.Count);
        }


        IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
        {
            for (int i = 0; i < this.m_Length; i++) {
                yield return this.m_Array[this.m_Start + i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<byte>)this).GetEnumerator();
        }


        public override string ToString()
        {
            return $"<Start = {this.m_Start}, Length = {this.m_Length}, MaxLength = {this.MaxLength}>";
        }



        public ByteBuffer(int initMaxLength = 64)
        {
            this.m_Array = new byte[initMaxLength];
            this.m_Start = 0;
            this.m_Length = 0;
        }

        void IReusable.Reinitialize()
        {
            // Do nothing
        }

        void IReusable.Recycle()
        {
            this.m_Start = 0;
            this.m_Length = 0;
        }
    }
}
