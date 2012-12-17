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
    internal static class SpotifyMarshalling
    {
        // This represents the sp_subscribers struct in libspotify.
        //
        // It looks roughly like this:
        // struct
        // {
        //     int count;
        //     char *subscribers[1];
        // }
        //
        // In actual fact, the array might have variable length, specified
        // by count.
        struct SpotifySubscribers
        {
            // The size of the array.
            public int Count;
            // The first (index 0) item in the array.
            public IntPtr FirstSubscriber;
        }

        public static string[] SubscribersToStrings(IntPtr aSubscribers)
        {
            // This is pretty painful.
            // Assumptions
            //     * C int is 32-bit. (This assumption is pervasive in P/Invoke code and
            //       not portable, e.g. to ILP64 systems, but it holds for every current
            //       system we might care about: Windows/Linux/Mac x86/x64, iOs/Android/
            //       Linux arm.)
            //     * Structs may not have padding before the first element. (Guaranteed by C.)
            //     * Arrays may not have padding before the first element. (Guaranteed by C?)
            //     * An array of pointers has the same alignment requirement as a single pointer. (?)
            // First of all, find the offset of the first item of the array inside the structure.
            var structOffset = (int)Marshal.OffsetOf(typeof(SpotifySubscribers), "FirstSubscriber");

            // Construct a pointer to the first item of the array.
            var arrayPtr = aSubscribers + structOffset;

            // Extract Count. I'm not 100% it's safe to use Marshal.PtrToStructure here, so
            // I'm using Marshal.Copy to extract the count into an array.
            int[] countArray = new int[1];
            Marshal.Copy(aSubscribers, countArray, 0, 1);
            int count = countArray[0];

            // Copy the array content into a managed array.
            IntPtr[] utf8Strings = new IntPtr[count];
            Marshal.Copy(arrayPtr, utf8Strings, 0, count);

            // Finally convert the strings to managed strings.
            return utf8Strings.Select(Utf8ToString).ToArray();
        }

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

        public static void CheckError(SpotifyError aError)
        {
            if (aError == SpotifyError.Ok) return;
            string message = Utf8ToString(NativeMethods.sp_error_message(aError));
            throw new SpotifyException(aError, message);
        }
    }

    [Serializable]
    public class SpotifyException : Exception
    {
        public SpotifyError Error { get; private set; }
        public SpotifyException(SpotifyError error)
        {
            Error = error;
        }
        public SpotifyException(SpotifyError error, string message) : base(message)
        {
            Error = error;
        }
        protected SpotifyException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    public partial class SpotifySession
    {
        IntPtr _handle;
        internal SpotifySession(IntPtr handle) { _handle = handle; }
        ~SpotifySession()
        {
            Dispose(false);
        }
        void Release()
        {
            NativeMethods.sp_session_release(_handle);
        }
        void CheckDisposed()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException("SpotifySession");
            }
        }
        void Dispose(bool disposing)
        {
            if (_handle != IntPtr.Zero)
            {
                Release();
                _handle = IntPtr.Zero;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
/*
    public partial class SpotifyPlaylist
    {
        IntPtr _handle;
        internal SpotifyPlaylist(IntPtr handle) { _handle = handle; }
        private ~SpotifyPlaylist()
        {
            Dispose(false);
        }
        void AddRef()
        {
            NativeMethods.sp_playlist_add_ref(_handle);
        }
        void Release()
        {
            NativeMethods.sp_playlist_release(_handle);
        }
        void CheckDisposed()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException("SpotifyPlaylist");
            }
        }
        void Dispose(bool disposing)
        {
            if (_handle != IntPtr.Zero)
            {
                Release();
                _handle = IntPtr.Zero;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }



        public void Rename(string new_name)
        {
            using (var utf8_new_name = SpotifyMarshalling.StringToUtf8(new_name))
            {
                NativeMethods.sp_playlist_
                var error = NativeMethods.sp_playlist_rename(_handle, utf8_new_name.IntPtr);

                SpotifyMarshalling.CheckError(error);
            }
        }
    }
 * */
}
