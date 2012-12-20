using System;

namespace SpotifySharp
{
    public delegate void ArtistBrowseComplete(ArtistBrowse @result);

    public sealed partial class ArtistBrowse : IDisposable
    {
        internal static readonly ManagedWrapperTable<ArtistBrowse> BrowseTable = new ManagedWrapperTable<ArtistBrowse>(x=>new ArtistBrowse(x));
        internal static readonly ManagedListenerTable<ArtistBrowseComplete> ListenerTable = new ManagedListenerTable<ArtistBrowseComplete>();

        IntPtr ListenerToken { get; set; }

        static void ArtistBrowseComplete(IntPtr result, IntPtr userdata)
        {
            var browse = BrowseTable.GetUniqueObject(result);
            var callback = ListenerTable.GetObject(userdata);
            callback(browse);
        }

        static readonly artistbrowse_complete_cb ArtistBrowseCompleteDelegate = ArtistBrowseComplete;

        public static ArtistBrowse Create(SpotifySession session, Artist artist, ArtistBrowseType type, ArtistBrowseComplete callback)
        {
            IntPtr listenerToken = ListenerTable.PutUniqueObject(callback);
            IntPtr ptr = NativeMethods.sp_artistbrowse_create(session._handle, artist._handle, type, ArtistBrowseCompleteDelegate, listenerToken);
            ArtistBrowse browse = BrowseTable.GetUniqueObject(ptr);
            browse.ListenerToken = listenerToken;
            return browse;
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            var error = NativeMethods.sp_artistbrowse_release(_handle);
            BrowseTable.ReleaseObject(_handle);
            ListenerTable.ReleaseObject(ListenerToken);
            _handle = IntPtr.Zero;
            SpotifyMarshalling.CheckError(error);
        }
    }
}