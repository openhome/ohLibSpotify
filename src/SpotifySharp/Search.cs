using System;

namespace SpotifySharp
{
    public delegate void SearchComplete(Search @result);
    public sealed partial class Search : IDisposable
    {
        internal static readonly ManagedWrapperTable<Search> SearchTable = new ManagedWrapperTable<Search>(x=>new Search(x));
        internal static readonly ManagedListenerTable<SearchComplete> ListenerTable = new ManagedListenerTable<SearchComplete>();

        IntPtr ListenerToken { get; set; }

        static void SearchComplete(IntPtr result, IntPtr userdata)
        {
            var browse = SearchTable.GetUniqueObject(result);
            var callback = ListenerTable.GetObject(userdata);
            callback(browse);
        }

        static readonly search_complete_cb SearchCompleteDelegate = SearchComplete;

        public static Search Create(
            SpotifySession session,
            string query,
            int trackOffset,
            int trackCount,
            int albumOffset,
            int albumCount,
            int artistOffset,
            int artistCount,
            int playlistOffset,
            int playlistCount,
            SearchType searchType,
            SearchComplete callback)
        {
            using (var utf8_query = SpotifyMarshalling.StringToUtf8(query))
            {
                IntPtr listenerToken = ListenerTable.PutUniqueObject(callback);
                IntPtr ptr = NativeMethods.sp_search_create(
                    session._handle,
                    utf8_query.IntPtr,
                    trackOffset,
                    trackCount,
                    albumOffset,
                    albumCount,
                    artistOffset,
                    artistCount,
                    playlistOffset,
                    playlistCount,
                    searchType,
                    SearchCompleteDelegate,
                    listenerToken);
                Search search = SearchTable.GetUniqueObject(ptr);
                search.ListenerToken = listenerToken;
                return search;
            }
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            var error = NativeMethods.sp_search_release(_handle);
            SearchTable.ReleaseObject(_handle);
            ListenerTable.ReleaseObject(ListenerToken);
            _handle = IntPtr.Zero;
            SpotifyMarshalling.CheckError(error);
        }
    }
}