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
        public int CmdPlaylist(string[] aArgs)
        {
            var pc = iSession.Playlistcontainer();

            if (aArgs.Length <= 1)
            {
                Console.WriteLine("playlist [playlist index]");
                return -1;
            }

            int index;
            if (!int.TryParse(aArgs[1], out index) || index<0 || index>=pc.NumPlaylists())
            {
                Console.WriteLine("Invalid index");
                return -1;
            }

            var playlist = pc.Playlist(index);

            int unseen = pc.GetUnseenTracks(playlist, null);

            Console.WriteLine(
                "Playlist {0} by {1}{2}{3}, {4} new tracks",
                playlist.Name(),
                playlist.Owner().DisplayName(),
                playlist.IsCollaborative() ? " (collaborative)" : "",
                playlist.HasPendingChanges() ? " with pending changes" : "",
                unseen
                );
            if (aArgs.Length == 3)
            {
                if (aArgs[2] == "new")
                {
                    if (unseen < 0)
                        return 1;
                    Track[] tracks = new Track[unseen];
                    pc.GetUnseenTracks(playlist, tracks);
                    for (int i = 0; i != unseen; ++i)
                    {
                        PrintTrack2(tracks[i], i);
                    }
                    return 1;
                }
                else if (aArgs[2] == "clear-unseen")
                {
                    pc.ClearUnseenTracks(playlist);
                }
            }
            for (int i = 0; i < playlist.NumTracks(); ++i)
            {
                Track track = playlist.Track(i);
                PrintTrack2(track, i);
            }
            return 1;
        }
        public void PrintTrack2(Track aTrack, int aIndex)
        {
            Console.WriteLine("{0,3}. {1} {2}{3} {4}",
                aIndex,
                Track.IsStarred(iSession, aTrack) ? "*" : " ",
                Track.IsLocal(iSession, aTrack) ? "local" : "     ",
                Track.IsAutolinked(iSession, aTrack) ? "autolinked" : "          ",
                aTrack.Name());
        }

    }
}
