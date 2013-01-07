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


    internal class UserDataTable<T>
    {
        object _monitor = new object();
        class Entry
        {
            public IntPtr NativeUserdata;
            public object ManagedUserdata;
            public T Listener;
        }
        readonly Dictionary<Tuple<IntPtr, object>, Entry> _managedTable = new Dictionary<Tuple<IntPtr, object>, Entry>();
        readonly Dictionary<IntPtr, Entry> _nativeTable = new Dictionary<IntPtr, Entry>();
        int _counter = 100; // Starting point is arbitrary, but should help distinguish real tokens from mistakes when debugging.
        public IntPtr PutListener(IntPtr owner, T listener, object userdata)
        {
            lock (_monitor)
            {
                _counter += 1;
                var token = (IntPtr) _counter;
                var managedKey = Tuple.Create(owner, userdata);
                if (_managedTable.ContainsKey(managedKey))
                {
                    throw new ArgumentException("This userdata is already registered.", "userdata");
                }
                var entry = new Entry
                {
                    NativeUserdata = token,
                    ManagedUserdata = userdata,
                    Listener = listener
                };

                _managedTable[managedKey] = entry;
                _nativeTable[token] = entry;

                return token;
            }
        }
        public void RemoveListener(IntPtr owner, object userdata)
        {
            lock (_monitor)
            {
                var managedKey = Tuple.Create(owner, userdata);
                Entry entry;
                if (!_managedTable.TryGetValue(managedKey, out entry))
                {
                    throw new KeyNotFoundException("RemoveListener: Key not found");
                }
                _managedTable.Remove(managedKey);
                _nativeTable.Remove(entry.NativeUserdata);
            }
        }
        public bool TryGetNativeUserdata(IntPtr owner, object managedUserdata, out IntPtr nativeUserdata)
        {
            lock (_monitor)
            {
                var managedKey = Tuple.Create(owner, managedUserdata);
                Entry entry;
                if (!_managedTable.TryGetValue(managedKey, out entry))
                {
                    nativeUserdata = IntPtr.Zero;
                    return false;
                }
                nativeUserdata = entry.NativeUserdata;
                return true;
            }
        }
        public bool TryGetListenerFromNativeUserdata(IntPtr nativeUserdata, out T listener, out object managedUserdata)
        {
            lock (_monitor)
            {
                Entry entry;
                if (!_nativeTable.TryGetValue(nativeUserdata, out entry))
                {
                    listener = default(T);
                    managedUserdata = null;
                    return false;
                }
                listener = entry.Listener;
                managedUserdata = entry.ManagedUserdata;
                return true;
            }
        }
    }
    internal class ManagedListenerTable<T>
    {
        object _monitor = new object();
        int _counter = 100;
        struct Entry
        {
            public T Listener;
            public object Userdata;
        }
        readonly Dictionary<IntPtr, Entry> _table = new Dictionary<IntPtr, Entry>();

        public IntPtr PutUniqueObject(T obj, object userdata)
        {
            lock (_monitor)
            {
                _counter += 1;
                _table[(IntPtr)_counter] = new Entry { Listener = obj, Userdata = userdata };
                return (IntPtr)_counter;
            }
        }

        public bool TryGetListener(IntPtr ptr, out T listener, out object userdata)
        {
            lock (_monitor)
            {
                Entry entry;
                if (_table.TryGetValue(ptr, out entry))
                {
                    listener = entry.Listener;
                    userdata = entry.Userdata;
                    return true;
                }
                listener = default(T);
                userdata = null;
                return false;
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



    //public delegate void ImageLoaded(Image @image);
    //public delegate void InboxPostComplete(Inbox @result);
}
