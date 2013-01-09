using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpotifySharp;

namespace SpShellSharp
{
    class PlaylistManager
    {
        SpotifySession iSession;
        IConsoleReader iConsoleReader;
        readonly Browser iBrowser;
        bool iSubscriptionsUpdated;

        public PlaylistManager(SpotifySession aSession, IConsoleReader aConsoleReader, Browser aBrowser)
        {
            iSession = aSession;
            iConsoleReader = aConsoleReader;
            iBrowser = aBrowser;
        }

        public int CmdPlaylists(string[] aArgs)
        {
            var pc = iSession.Playlistcontainer();
            Console.WriteLine("{0} entries in the container", pc.NumPlaylists());
            int level = 0;
            Action indent = () => { for (int j=0; j!=level; ++j) Console.Write("\t"); };
            for (int i = 0; i != pc.NumPlaylists(); ++i)
            {
                switch (pc.PlaylistType(i))
                {
                    case PlaylistType.Playlist:
                        Console.Write("{0}. ", i);
                        indent();
                        var pl = pc.Playlist(i);
                        Console.Write(pl.Name());
                        if (iSubscriptionsUpdated)
                            Console.Write(" ({0} subscribers)", pl.NumSubscribers());
                        int unseen = pc.GetUnseenTracks(pl, null);
                        if (unseen != 0)
                        {
                            Console.Write(" ({0} new)", unseen);
                        }
                        Console.WriteLine();
                        break;
                    case PlaylistType.StartFolder:
                        Console.Write("{0}. ", i);
                        indent();
                        Console.WriteLine("Folder: {0} with id {1}", pc.PlaylistFolderName(i), pc.PlaylistFolderId(i));
                        level++;
                        break;
                    case PlaylistType.EndFolder:
                        level--;
                        Console.Write("{0}. ", i);
                        indent();
                        Console.WriteLine("End folder with id {0}", pc.PlaylistFolderId(i));
                        break;
                    case PlaylistType.Placeholder:
                        Console.Write("{0}. Placeholder", i);
                        break;

                }
            }
            return 1;
        }

    }
}
