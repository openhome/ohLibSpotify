using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SpotifySharp
{
    internal class Utf8String : IDisposable
    {
        IntPtr iPtr;
        public IntPtr IntPtr { get { return iPtr; } }
        public int BufferLength { get { return iBufferSize; } }
        int iBufferSize;
        public Utf8String(int aBufferSize)
        {
            if (aBufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("aBufferSize", "Argument must be positive.");
            }
            iPtr = Marshal.AllocHGlobal(aBufferSize);
            iBufferSize = aBufferSize;
        }
        public Utf8String(string aValue)
        {
            if (aValue == null)
            {
                iPtr = IntPtr.Zero;
            }
            else
            {
                byte[] bytes = Encoding.UTF8.GetBytes(aValue);
                iPtr = Marshal.AllocHGlobal(bytes.Length + 1);
                Marshal.Copy(bytes, 0, iPtr, bytes.Length);
                Marshal.WriteByte(iPtr, bytes.Length, 0);
                iBufferSize = bytes.Length + 1;
            }
        }
        public void ReallocIfSmaller(int aMinLength)
        {
            if (iPtr == IntPtr.Zero)
            {
                throw new ObjectDisposedException("Utf8String");
            }
            if (iBufferSize <= aMinLength)
            {
                iPtr = Marshal.ReAllocHGlobal(iPtr, (IntPtr)aMinLength);
                iBufferSize = aMinLength;
            }
        }
        public string Value
        {
            get
            {
                if (iPtr == IntPtr.Zero)
                {
                    throw new ObjectDisposedException("Utf8String");
                }
                return SpotifyMarshalling.Utf8ToString(iPtr);
            }
        }
        public void Dispose()
        {
            if (iPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(iPtr);
                iPtr = IntPtr.Zero;
            }
        }

        public string GetString(int aStringLengthBuffer)
        {
            return Value; // TODO: Include \0 characters.
        }
    }
}