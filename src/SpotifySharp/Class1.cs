using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SpotifySharp
{
    /*class NativeMethods
    {
         // SP_LIBEXPORT(int) sp_link_as_string(sp_link *link, char *buffer, int buffer_size);
        [DllImport("spotify")]
        internal static extern int sp_link_as_string(IntPtr link, IntPtr buffer, int buffer_size);
    }*/

    class Link
    {
        IntPtr iHandle;
        string AsString()
        {
            int buffer_string_length = NativeMethods.sp_link_as_string(iHandle, IntPtr.Zero, 0);
            using (var buffer = SpotifyMarshalling.AllocBuffer(buffer_string_length + 1))
            {
                NativeMethods.sp_link_as_string(iHandle, buffer.IntPtr, buffer_string_length + 1);
                return buffer.Value;
            }
        }
    }


    internal class Utf8String : IDisposable
    {
        IntPtr iPtr;
        public IntPtr IntPtr { get { return iPtr; } }
        public Utf8String(int aBufferSize)
        {
            if (aBufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("aBufferSize", "Argument must be positive.");
            }
            iPtr = Marshal.AllocHGlobal(aBufferSize);
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
    }
    internal static class SpotifyMarshalling
    {
        public static Utf8String StringToUtf8(string aString)
        {
            return new Utf8String(aString);
        }
        public static Utf8String AllocBuffer(int aBufferSize)
        {
            return new Utf8String(aBufferSize);
        }
        public static string Utf8ToString(IntPtr aUtf8)
        {
            if (aUtf8 == IntPtr.Zero)
                return null;
            int len = 0;
            while (Marshal.ReadByte(aUtf8, len) != 0)
                len++;
            if (len == 0)
                return "";
            byte[] array = new byte[len];
            Marshal.Copy(aUtf8, array, 0, len);
            return Encoding.UTF8.GetString(array);
        }


    }
}
