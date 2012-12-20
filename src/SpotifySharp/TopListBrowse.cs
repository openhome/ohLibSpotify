﻿using System;

namespace SpotifySharp
{
    public delegate void TopListBrowseComplete(TopListBrowse @result);
    public sealed partial class TopListBrowse : IDisposable
    {
        internal static readonly ManagedWrapperTable<TopListBrowse> BrowseTable = new ManagedWrapperTable<TopListBrowse>(x=>new TopListBrowse(x));
        internal static readonly ManagedListenerTable<TopListBrowseComplete> ListenerTable = new ManagedListenerTable<TopListBrowseComplete>();

        IntPtr ListenerToken { get; set; }

        static void TopListBrowseComplete(IntPtr result, IntPtr userdata)
        {
            var browse = BrowseTable.GetUniqueObject(result);
            var callback = ListenerTable.GetObject(userdata);
            callback(browse);
        }

        static readonly toplistbrowse_complete_cb TopListBrowseCompleteDelegate = TopListBrowseComplete;

        public static TopListBrowse Create(SpotifySession session, TopListType type, TopListRegion region, string username, TopListBrowseComplete callback)
        {
            using (var utf8_username = SpotifyMarshalling.StringToUtf8(username))
            {
                IntPtr listenerToken = ListenerTable.PutUniqueObject(callback);
                IntPtr ptr = NativeMethods.sp_toplistbrowse_create(session._handle, type, region, utf8_username.IntPtr, TopListBrowseCompleteDelegate, listenerToken);
                TopListBrowse browse = BrowseTable.GetUniqueObject(ptr);
                browse.ListenerToken = listenerToken;
                return browse;
            }
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            var error = NativeMethods.sp_toplistbrowse_release(_handle);
            BrowseTable.ReleaseObject(_handle);
            ListenerTable.ReleaseObject(ListenerToken);
            _handle = IntPtr.Zero;
            SpotifyMarshalling.CheckError(error);
        }
    }
}