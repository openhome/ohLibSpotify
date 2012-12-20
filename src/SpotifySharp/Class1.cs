using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SpotifySharp
{
    /*class NativeMethods
    {
         // SP_LIBEXPORT(int) sp_link_as_string(sp_link *link, char *buffer, int buffer_size);
        [DllImport("spotify")]
        internal static extern int sp_link_as_string(IntPtr link, IntPtr buffer, int buffer_size);
    }*/

    /*class Link
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
    }*/


    internal class ManagedListenerTable<T>
    {
        object _monitor = new object();
        int _counter = 100;
        readonly Dictionary<IntPtr, T> _table = new Dictionary<IntPtr, T>();

        public IntPtr PutUniqueObject(T obj)
        {
            lock (_monitor)
            {
                _counter += 1;
                _table[(IntPtr)_counter] = obj;
                return (IntPtr)_counter;
            }
        }

        public T GetObject(IntPtr ptr)
        {
            lock (_monitor)
            {
                T retval;
                if (_table.TryGetValue(ptr, out retval))
                {
                    return retval;
                }
                return default(T);
            }
        }

        public void ReleaseObject(IntPtr ptr)
        {
            lock (_monitor)
            {
                _table.Remove(ptr);
            }
        }
    }

    internal class ManagedWrapperTable<T>
    {
        readonly Func<IntPtr, T> _constructor;
        object _monitor = new object();
        readonly Dictionary<IntPtr, T> _table = new Dictionary<IntPtr, T>();

        public ManagedWrapperTable(Func<IntPtr,T> constructor)
        {
            _constructor = constructor;
        }

        public T GetUniqueObject(IntPtr ptr)
        {
            lock (_monitor)
            {
                T retval;
                if (_table.TryGetValue(ptr, out retval))
                {
                    return retval;
                }
                retval = _constructor(ptr);
                _table[ptr] = retval;
                return retval;
            }
        }

        public void ReleaseObject(IntPtr ptr)
        {
            lock (_monitor)
            {
                _table.Remove(ptr);
            }
        }
    }



    public partial class Track
    {
        public static void SetStarred(SpotifySession session, IEnumerable<Track> tracks, bool star)
        {
            throw new NotImplementedException();
            // TODO: Need to fix NativeMethods.sp_track_set_starred to marshal using an array.
            //NativeMethods.sp_track_set_starred(
        }
    }

    public delegate void ImageLoaded(Image @image);
    public delegate void InboxPostComplete(Inbox @result);
}
