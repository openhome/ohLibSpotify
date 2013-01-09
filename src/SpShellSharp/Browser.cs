using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpotifySharp;

namespace SpShellSharp
{
    class Browser
    {
        SpotifySession iSession;
        IMetadataWaiter iMetadataWaiter;
        IConsoleReader iConsoleReader;
        Track iTrackBrowse;
        Playlist iPlaylistBrowse;
        BrowsingPlaylistListener iPlaylistListener;
        bool iListeningForPlaylist;

        class BrowsingPlaylistListener : PlaylistListener
        {
            Browser iBrowser;

            public BrowsingPlaylistListener(Browser aBrowser)
            {
                iBrowser = aBrowser;
            }

            public override void TracksAdded(Playlist pl, Track[] tracks, int position, object userdata)
            {
                Console.WriteLine("\t{0} tracks added", tracks.Length);
            }
            public override void TracksRemoved(Playlist pl, int[] tracks, object userdata)
            {
                Console.WriteLine("\t{0} tracks removed", tracks.Length);
            }
            public override void TracksMoved(Playlist pl, int[] tracks, int new_position, object userdata)
            {
                Console.WriteLine("\t{0} tracks moved", tracks.Length);
            }
            public override void PlaylistRenamed(Playlist pl, object userdata)
            {
                Console.WriteLine("List name: {0}", pl.Name());
            }
            public override void PlaylistStateChanged(Playlist pl, object userdata)
            {
                iBrowser.PlaylistBrowseTry();
            }
        }
        public Browser(SpotifySession aSession, IMetadataWaiter aMetadataWaiter, IConsoleReader aConsoleReader)
        {
            iSession = aSession;
            iMetadataWaiter = aMetadataWaiter;
            iConsoleReader = aConsoleReader;
            iPlaylistListener = new BrowsingPlaylistListener(this);
        }
        public int CmdBrowse(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: browse <spotify-url>");
                return -1;
            }
            var link = Link.CreateFromString(args[1]);
            if (link == null)
            {
                Console.Error.WriteLine("Not a spotify link");
                return -1;
            }
            switch (link.Type())
            {
                default:
                    Console.Error.WriteLine("Can not handle link");
                    link.Release();
                    return -1;
                case LinkType.Album:
                    AlbumBrowse.Create(iSession, link.AsAlbum(), BrowseAlbumCallback, null);
                    break;
                case LinkType.Artist:
                    ArtistBrowse.Create(iSession, link.AsArtist(), ArtistBrowseType.Full, BrowseArtistCallback, null);
                    break;
                case LinkType.Localtrack:
                case LinkType.Track:
                    iTrackBrowse = link.AsTrack();
                    iMetadataWaiter.AddMetadataUpdatedCallback(TrackBrowseTry);
                    iTrackBrowse.AddRef();
                    TrackBrowseTry();
                    break;
                case LinkType.Playlist:
                    BrowsePlaylist(Playlist.Create(iSession, link));
                    break;
            }
            link.Release();
            return 0;
        }

        void StartingListeningForPlaylistChanges()
        {
            if (!iListeningForPlaylist)
            {
                iMetadataWaiter.AddMetadataUpdatedCallback(PlaylistBrowseTry);
                iListeningForPlaylist = true;
            }
        }

        void StopListeningForPlaylistChanges()
        {
            if (iListeningForPlaylist)
            {
                iMetadataWaiter.RemoveMetadataUpdatedCallback(PlaylistBrowseTry);
                iListeningForPlaylist = false;
            }
        }

        void BrowsePlaylist(Playlist aPlaylist)
        {
            Console.WriteLine(">BrowsePlaylist");
            iPlaylistBrowse = aPlaylist;
            aPlaylist.AddCallbacks(iPlaylistListener, null);
            PlaylistBrowseTry();
            Console.WriteLine("<BrowsePlaylist");
        }

        void PlaylistBrowseTry()
        {
            Console.WriteLine(">PlaylistBrowseTry");
            StartingListeningForPlaylistChanges();
            if (!iPlaylistBrowse.IsLoaded())
            {
                Console.WriteLine("\tPlaylist not loaded");
                Console.WriteLine("<PlaylistBrowseTry");
                return;
            }

            int tracks = iPlaylistBrowse.NumTracks();
            for (int i = 0; i != tracks; ++i)
            {
                Track t = iPlaylistBrowse.Track(i);
                if (!t.IsLoaded())
                {
                    Console.WriteLine("<PlaylistBrowseTry");
                    return;
                }
            }

            Console.WriteLine("\tPlaylist and metadata loaded");

            for (int i = 0; i != tracks; ++i)
            {
                Track t = iPlaylistBrowse.Track(i);
                Console.Write(" {0,5}: ", i + 1);
                PrintTrack(t);
            }

            iPlaylistBrowse.RemoveCallbacks(iPlaylistListener, null);
            StopListeningForPlaylistChanges();
            iPlaylistBrowse.Release();
            iPlaylistBrowse = null;
            iConsoleReader.RequestInput("> ");
            Console.WriteLine("<PlaylistBrowseTry");
        }


        void TrackBrowseTry()
        {
            try
            {
                iTrackBrowse.Error();
                PrintTrack(iTrackBrowse);
            }
            catch (SpotifyException e)
            {
                switch (e.Error)
                {
                    case SpotifyError.IsLoading:
                        return;
                    default:
                        Console.WriteLine("Unable to resolve track: {0}", e.Message);
                        break;
                }
            }
            iMetadataWaiter.RemoveMetadataUpdatedCallback(TrackBrowseTry);
            iConsoleReader.RequestInput("> ");
            iTrackBrowse.Release();
            iTrackBrowse = null;
        }

        void PrintTrack(Track aTrack)
        {
            int duration = aTrack.Duration();
            Console.Write(" {0} ", Track.IsStarred(iSession, aTrack) ? "*" : " ");
            Console.Write("Track {0} [{1}:{2:D02}] has {3} artist(s), {4}% popularity",
                aTrack.Name(),
                duration / 60000,
                (duration / 1000) % 60,
                aTrack.NumArtists(),
                aTrack.Popularity());
            if (aTrack.Disc() != 0)
            {
                Console.Write(", {0} on disc {1}",
                    aTrack.Index(),
                    aTrack.Disc());
            }
            for (int i = 0; i < aTrack.NumArtists(); ++i)
            {
                var artist = aTrack.Artist(i);
                Console.Write("\tArtist {0}: {1}", i + 1, artist.Name());
            }
            var link = Link.CreateFromTrack(aTrack, 0);
            Console.WriteLine("\t\t{0}", link.AsString());
            link.Release();
        }

        string Truncate(string s, int length)
        {
            return s.Length <= length ? s : (s.Substring(0, length) + "...");
        }

        void BrowseAlbumCallback(AlbumBrowse aResult, object aUserdata)
        {
            try
            {
                aResult.Error();
                PrintAlbumBrowse(aResult);
            }
            catch (SpotifyException e)
            {
                Console.Error.WriteLine("Failed to browse album: {0}", e.Message);
            }
            aResult.Dispose();
            iConsoleReader.RequestInput("> ");
        }

        void PrintAlbumBrowse(AlbumBrowse aResult)
        {
            Console.WriteLine("Album browse of \"{0}\" ({1})", aResult.Album().Name(), aResult.Album().Year());
            for (int i = 0; i != aResult.NumCopyrights(); ++i)
            {
                Console.WriteLine("  Copyright: {0}", aResult.Copyright(i));
            }
            Console.WriteLine("  Tracks: {0}", aResult.NumTracks());
            Console.WriteLine("  Review: {0}", Truncate(aResult.Review(), 60));
            Console.WriteLine();
            for (int i = 0; i != aResult.NumTracks(); ++i)
            {
                PrintTrack(aResult.Track(i));
            }
            Console.WriteLine();
        }

        void BrowseArtistCallback(ArtistBrowse aResult, object aUserdata)
        {
            try
            {
                aResult.Error();
                PrintArtistBrowse(aResult);
            }
            catch (SpotifyException e)
            {
                Console.Error.WriteLine("Failed to browse artist: {0}", e.Message);
            }
            aResult.Dispose();
            iConsoleReader.RequestInput("> ");
        }

        void PrintArtistBrowse(ArtistBrowse aResult)
        {
            Console.WriteLine("Artist browse of \"{0}\"", aResult.Artist().Name());
            for (int i = 0; i != aResult.NumSimilarArtists(); ++i)
            {
                Console.WriteLine("  Similar artist: {0}", aResult.SimilarArtist(i).Name());
            }
            Console.WriteLine("  Portraits: {0}", aResult.NumPortraits());
            Console.WriteLine("  Tracks: {0}", aResult.NumTracks());
            Console.WriteLine("  Biography: {0}", Truncate(aResult.Biography(),60));
            Console.WriteLine();
            for (int i = 0; i != aResult.NumTracks(); ++i)
            {
                PrintTrack(aResult.Track(i));
            }
            Console.WriteLine();
        }
    }
}
