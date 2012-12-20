using System;

namespace SpotifySharp
{
    public delegate void AlbumBrowseComplete(AlbumBrowse @result);
    public sealed partial class AlbumBrowse : IDisposable
    {
        internal static readonly ManagedWrapperTable<AlbumBrowse> BrowseTable = new ManagedWrapperTable<AlbumBrowse>(x=>new AlbumBrowse(x));
        internal static readonly ManagedListenerTable<AlbumBrowseComplete> ListenerTable = new ManagedListenerTable<AlbumBrowseComplete>();

        IntPtr ListenerToken { get; set; }

        static void AlbumBrowseComplete(IntPtr result, IntPtr userdata)
        {
            var browse = BrowseTable.GetUniqueObject(result);
            var callback = ListenerTable.GetObject(userdata);
            callback(browse);
        }

        static readonly albumbrowse_complete_cb AlbumBrowseCompleteDelegate = AlbumBrowseComplete;

        public static AlbumBrowse Create(SpotifySession session, Album album, AlbumBrowseComplete callback)
        {
            IntPtr listenerToken = ListenerTable.PutUniqueObject(callback);
            IntPtr ptr = NativeMethods.sp_albumbrowse_create(session._handle, album._handle, AlbumBrowseCompleteDelegate, listenerToken);
            AlbumBrowse browse = BrowseTable.GetUniqueObject(ptr);
            browse.ListenerToken = listenerToken;
            return browse;
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            var error = NativeMethods.sp_albumbrowse_release(_handle);
            BrowseTable.ReleaseObject(_handle);
            ListenerTable.ReleaseObject(ListenerToken);
            _handle = IntPtr.Zero;
            SpotifyMarshalling.CheckError(error);
        }
    }
}