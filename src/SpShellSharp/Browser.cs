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
        public Browser(SpotifySession aSession, IMetadataWaiter aMetadataWaiter, IConsoleReader aConsoleReader)
        {
            iSession = aSession;
            iMetadataWaiter = aMetadataWaiter;
            iConsoleReader = aConsoleReader;
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
                    //iMetadataWaiter.WaitForMetadataUpdate(TrackBrowseTry);
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

        void BrowsePlaylist(Playlist aCreate)
        {
            throw new NotImplementedException();
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
                        iMetadataWaiter.WaitForMetadataUpdate(TrackBrowseTry);
                        return;
                    default:
                        Console.WriteLine("Unable to resolve track: {0}", e.Message);
                        break;
                }
            }
            iConsoleReader.RequestInput("> ");
            iTrackBrowse.Release();
            iTrackBrowse = null;
        }

        void PrintTrack(Track aTrack)
        {
            int duration = aTrack.Duration();
            Console.Write(" {0} ", Track.IsStarred(iSession, aTrack) ? "*" : " ");
            Console.Write("Track {0} [{1}:{2:02d}] has {3} artist(s), {4}% popularity",
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

        void BrowseAlbumCallback(AlbumBrowse aResult, object aUserdata)
        {
            throw new NotImplementedException();
        }

        void BrowseArtistCallback(ArtistBrowse aResult, object aUserdata)
        {
            throw new NotImplementedException();
        }
    }
}
