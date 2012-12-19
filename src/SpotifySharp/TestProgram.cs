using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SpotifySharp
{
    static class WaveWriter
    {
        static byte[] WriteBytes(Action<BinaryWriter> aWriteAction)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    aWriteAction(writer);
                }
                return stream.ToArray();
            }
        }

        class FmtBlock
        {
            public ushort AudioFormat;
            public ushort NumChannels;
            public uint SampleRate;
            public uint ByteRate;
            public ushort BlockAlign;
            public ushort BitsPerSample;

            public void Write(BinaryWriter aWriter)
            {
                aWriter.Write(AudioFormat);
                aWriter.Write(NumChannels);
                aWriter.Write(SampleRate);
                aWriter.Write(ByteRate);
                aWriter.Write(BlockAlign);
                aWriter.Write(BitsPerSample);
            }

        }
        public static void WriteWave(BinaryWriter aWriter, int aSampleRate, int aChannels, int aBytesPerSample, byte[] aPcmData)
        {
            RiffWriter riff = new RiffWriter(new RiffId("WAVE"));
            riff.AddDataChunk(
                new RiffId("fmt "),
                WriteBytes(
                    new FmtBlock
                    {
                        AudioFormat = 1,
                        NumChannels = (ushort)aChannels,
                        SampleRate = (ushort)aSampleRate,
                        ByteRate = (uint) (aChannels * aSampleRate * aBytesPerSample),
                        BitsPerSample = (ushort) (aBytesPerSample * 8),
                        BlockAlign = (ushort) (aChannels * aBytesPerSample)
                    }.Write));
            riff.AddDataChunk(
                new RiffId("data"),
                aPcmData);
            riff.WriteToFile(aWriter);
        }
    }

    class TestProgram
    {
        static string FormatCSharpString(string aString)
        {
            StringBuilder sb = new StringBuilder(aString.Length * 2);
            sb.Append("\"");
            for (int i = 0; i != aString.Length; ++i)
            {
                char ch = aString[i];
                switch (ch)
                {
                    case '\r': sb.Append(@"\r"); continue;
                    case '\n': sb.Append(@"\n"); continue;
                    case '\f': sb.Append(@"\f"); continue;
                    case '\t': sb.Append(@"\t"); continue;
                    case '\\': sb.Append(@"\\"); continue;
                    case '\"': sb.Append(@"\"""); continue;
                    case '\a': sb.Append(@"\a"); continue;
                    case '\b': sb.Append(@"\b"); continue;
                    case '\v': sb.Append(@"\v"); continue;
                    default:
                        if (char.IsControl(ch))
                        {
                            sb.Append(@"\x");
                            sb.Append(((int)ch).ToString("X4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        class CallbackAgent
        {
            sp_session_callbacks iSessionCallbacks;
            ManualResetEvent iEv;
            public bool Finished { get; private set; }
            Queue<string> iTracksToLoad = new Queue<string>();
            byte[] iOutputBuffer = new byte[128 * 1024 * 1024];
            byte[] iTempBuffer = new byte[8192];
            int iOutputIndex = 0;

            public byte[] Buffer { get { return iOutputBuffer.Take(iOutputIndex).ToArray(); } }

            public void QueueTrack(string aTrackUri)
            {
                lock (iTracksToLoad)
                {
                    iTracksToLoad.Enqueue(aTrackUri);
                }
                iEv.Set();
            }

            public CallbackAgent(ManualResetEvent ev)
            {
                iEv = ev;
                iSessionCallbacks = new sp_session_callbacks();
                iSessionCallbacks.logged_in = logged_in;
                iSessionCallbacks.logged_in = logged_in;
                iSessionCallbacks.logged_out = logged_out;
                iSessionCallbacks.metadata_updated = metadata_updated;
                iSessionCallbacks.connection_error = connection_error;
                iSessionCallbacks.message_to_user = message_to_user;
                iSessionCallbacks.notify_main_thread = notify_main_thread;
                iSessionCallbacks.music_delivery = music_delivery;
                iSessionCallbacks.play_token_lost = play_token_lost;
                iSessionCallbacks.log_message = log_message;
                iSessionCallbacks.end_of_track = end_of_track;
                iSessionCallbacks.streaming_error = streaming_error;
                iSessionCallbacks.userinfo_updated = userinfo_updated;
                iSessionCallbacks.start_playback = start_playback;
                iSessionCallbacks.stop_playback = stop_playback;
                iSessionCallbacks.get_audio_buffer_stats = get_audio_buffer_stats;
                iSessionCallbacks.offline_status_updated = offline_status_updated;
                iSessionCallbacks.offline_error = offline_error;
                iSessionCallbacks.credentials_blob_updated = credentials_blob_updated;
                iSessionCallbacks.connectionstate_updated = connectionstate_updated;
                iSessionCallbacks.scrobble_error = scrobble_error;
                iSessionCallbacks.private_session_mode_changed = private_session_mode_changed;
            }
            public sp_session_callbacks GetCallbacksStruct()
            {
                return iSessionCallbacks;
            }
            void logged_in(IntPtr session, SpotifyError error)
            {
                Console.WriteLine("logged_in(session, {0})", error);
            }
            void logged_out(IntPtr session)
            {
                Console.WriteLine("logged_out(session)");
            }
            void dump_playlist(IntPtr session, IntPtr playlist)
            {
                int numTracks = NativeMethods.sp_playlist_num_tracks(playlist);
                for (int i = 0; i != numTracks; ++i)
                {
                    IntPtr track = NativeMethods.sp_playlist_track(playlist, i);
                    if (!NativeMethods.sp_track_is_loaded(track))
                    {
                        Console.WriteLine("    Track #{0}: (not loaded)", i);
                        continue;
                    }
                    string name = SpotifyMarshalling.Utf8ToString(NativeMethods.sp_track_name(track));
                    var availability = NativeMethods.sp_track_get_availability(session, track);
                    Console.WriteLine("    Track #{0}: {1} [{2}]", i, FormatCSharpString(name), availability);
                }
            }
            void metadata_updated(IntPtr session)
            {
                Console.WriteLine("metadata_updated(session)");
                IntPtr pc = NativeMethods.sp_session_playlistcontainer(session);
                if (!NativeMethods.sp_playlistcontainer_is_loaded(pc))
                {
                    Console.WriteLine("Container not loaded yet.");
                    return;
                }
                Console.WriteLine("PLAYLISTS:");
                int numLists = NativeMethods.sp_playlistcontainer_num_playlists(pc);
                for (int i = 0; i != numLists; ++i)
                {
                    var playlistType = NativeMethods.sp_playlistcontainer_playlist_type(pc, i);
                    switch (playlistType)
                    {
                        case PlaylistType.Playlist:
                            IntPtr playlist = NativeMethods.sp_playlistcontainer_playlist(pc, i);
                            if (!NativeMethods.sp_playlist_is_loaded(playlist))
                            {
                                Console.WriteLine("Playlist (not loaded)");
                                break;
                            }
                            string name = SpotifyMarshalling.Utf8ToString(NativeMethods.sp_playlist_name(playlist));
                            Console.WriteLine("Playlist {0}", FormatCSharpString(name));
                            dump_playlist(session, playlist);
                            break;
                        case PlaylistType.StartFolder:
                        case PlaylistType.EndFolder:
                            string startEnd = playlistType == PlaylistType.StartFolder ? "start" : "end";
                            using (var utf8Buffer = SpotifyMarshalling.AllocBuffer(256))
                            {
                                var sperror = NativeMethods.sp_playlistcontainer_playlist_folder_name(pc, i, utf8Buffer.IntPtr, 256);
                                if (sperror != SpotifyError.Ok)
                                {
                                    Console.WriteLine("Unknown folder {0} {1}", startEnd, sperror);
                                }
                                else
                                {
                                    Console.WriteLine("Folder {0} {1}", startEnd, FormatCSharpString(utf8Buffer.Value));
                                }
                            }
                            break;
                        case PlaylistType.Placeholder:
                            Console.WriteLine("Placeholder");
                            break;
                        default:
                            Console.WriteLine("Bad value");
                            break;
                    }
                }
                Console.WriteLine("END OF PLAYLISTS");
                Console.WriteLine("");
            }
            public void LoadTracks(IntPtr aSession)
            {
                var session = aSession;
                lock (iTracksToLoad)
                {
                    while (iTracksToLoad.Count > 0)
                    {
                        string trackUri = iTracksToLoad.Dequeue();
                        using (var utf8TrackUri = SpotifyMarshalling.StringToUtf8(trackUri))
                        {
                            IntPtr link = NativeMethods.sp_link_create_from_string(utf8TrackUri.IntPtr);
                            IntPtr track = NativeMethods.sp_link_as_track(link);
                            var loadError = NativeMethods.sp_session_player_load(session, track);
                            Console.WriteLine("Tried to load, got: {0}", loadError);
                            if (loadError == SpotifyError.Ok)
                            {
                                var playError = NativeMethods.sp_session_player_play(session, true);
                                Console.WriteLine("Tried to play, got: {0}", playError);
                            }
                        }
                    }
                }
            }
            void connection_error(IntPtr session, SpotifyError error)
            {
                Console.WriteLine("connection_error(session, {0})", error);
            }
            void message_to_user(IntPtr session, IntPtr message)
            {
                Console.WriteLine("message_to_user(session, {0})", FormatCSharpString(SpotifyMarshalling.Utf8ToString(message)));
            }
            void notify_main_thread(IntPtr session)
            {
                Console.WriteLine("notify_main_thread(session)");
                iEv.Set();
            }
            int music_delivery(IntPtr session, ref AudioFormat format, IntPtr frames, int num_frames)
            {
                Console.WriteLine("music(session, fmt={0}, frame_ptr={1}, count={2}, channels={3}, rate={4}, type={5})", format, frames, num_frames, format.channels, format.sample_rate, format.sample_type);
                if (format.sample_type != SampleType.Int16NativeEndian)
                {
                    return num_frames;
                }
                int bytes_per_frame = 2 * format.channels;
                int bytes_delivered = bytes_per_frame * num_frames;
                Console.WriteLine("Delivered {0} bytes", bytes_delivered);
                if (bytes_delivered > iTempBuffer.Length)
                {
                    num_frames = iTempBuffer.Length / bytes_per_frame;
                    bytes_delivered = bytes_per_frame * num_frames;
                }
                Marshal.Copy(frames, iTempBuffer, 0, bytes_delivered);
                Array.Copy(iTempBuffer, 0, iOutputBuffer, iOutputIndex, bytes_delivered);
                iOutputIndex += bytes_delivered;
                Console.WriteLine("    Now at index {0}", iOutputIndex);
                return num_frames;
            }
            void play_token_lost(IntPtr session)
            {
                Console.WriteLine("play_token_lost(session)");
            }
            void log_message(IntPtr session, IntPtr data)
            {
                string message = SpotifyMarshalling.Utf8ToString(data);
                Console.WriteLine("log_message(session, data={0})", FormatCSharpString(SpotifyMarshalling.Utf8ToString(data)));
            }
            void end_of_track(IntPtr session)
            {
                Console.WriteLine("end_of_track");
            }
            void streaming_error(IntPtr session, SpotifyError error)
            {
                Console.WriteLine("streaming_error(session, error={0})", error);
            }
            void userinfo_updated(IntPtr session)
            {
                Console.WriteLine("userinfo_updated");
            }
            void start_playback(IntPtr session)
            {
                Console.WriteLine("start_playback");
            }
            void stop_playback(IntPtr session)
            {
                Console.WriteLine("stop_playback");
            }
            void get_audio_buffer_stats(IntPtr session, ref AudioBufferStats stats)
            {
                stats.stutter = 0;
                stats.samples = 4096;
                //Console.WriteLine("get_audio_buffer_stats");
            }
            void offline_status_updated(IntPtr session)
            {
                Console.WriteLine("offline_status_updated");
            }
            void offline_error(IntPtr session, SpotifyError error)
            {
                Console.WriteLine("offline_error(session, error={0})", error);
            }
            void credentials_blob_updated(IntPtr session, IntPtr blob)
            {
                Console.WriteLine("credentials_blob_updated(session, blob={0})", FormatCSharpString(SpotifyMarshalling.Utf8ToString(blob)));
            }
            void connectionstate_updated(IntPtr session)
            {
                Console.WriteLine("connectionstate_updated");
            }
            void scrobble_error(IntPtr session, SpotifyError error)
            {
                Console.WriteLine("scrobble_error(session, error={0})", error);
            }
            void private_session_mode_changed(IntPtr session, bool is_private)
            {
                Console.WriteLine("private_session_mode_changed(session, is_private={0})", is_private);
            }

            public void Stop()
            {
                Finished = true;
                iEv.Set();
            }
        }

        static void Main(string[] args)
        {
            ManualResetEvent ev = new ManualResetEvent(false);
            //CallbackAgent agent = new CallbackAgent(ev);
            byte[] appkey = File.ReadAllBytes("spotify_appkey.key");
            var callbacks = new Callbacks2(ev);
            var config = new SpotifySessionConfig
            {
                ApiVersion = 12,
                ApplicationKey = appkey,
                CacheLocation = "tmpdirabcd2",
                Listener = callbacks,
                CompressPlaylists = false,
                DontSaveMetadataForPlaylists = false,
                InitiallyUnloadPlaylists = false,
                SettingsLocation = "settingsdirwxyz2",
                UserAgent = ".NET test program 2"
            };
            using (var session = SpotifySession.Create(config))
            {
                Console.WriteLine("Created");
                Console.Write("username:");
                string username = Console.ReadLine();
                Console.Write("password:");
                string password = Console.ReadLine();

                session.Login(username, password, false, null);

                Console.WriteLine("Login started");

                bool finished = false;

                int timeout = 0;
                var consoleThread = new Thread(() =>
                {
                    while (true)
                    {
                        Console.WriteLine("Track URI:");
                        var uri = Console.ReadLine().Trim();
                        finished = true;
                        ev.Set();
                        break;
                        /*if (uri != "")
                        {
                            agent.QueueTrack(uri);
                        }
                        else
                        {
                            agent.Stop();
                        }*/
                    }
                });
                consoleThread.Start();

                while (!finished)
                {
                    ev.WaitOne(timeout);
                    timeout = 0;
                    while (timeout == 0)
                    {
                        session.ProcessEvents(ref timeout);
                        //NativeMethods.sp_session_process_events(session, ref timeout);
                    }
                    //agent.LoadTracks(session);

                }
            }

            /*var pcmData = agent.Buffer;

            using (var f = File.OpenWrite("soundout.wav"))
            using (var writer = new BinaryWriter(f))
            {
                WaveWriter.WriteWave(writer, 41000, 2, 2, pcmData);
            }
            File.WriteAllBytes("rawpcm", agent.Buffer);
            error = NativeMethods.sp_session_player_unload(session);
            Console.WriteLine("unload: {0}", error);
            error = NativeMethods.sp_session_release(session);
            Console.WriteLine("release: {0}", error);

            //Console.ReadLine();
            // Skip shutdown - libspotify.NET is a mess. 
            //error = libspotify.sp_session_release(ref session);
            //Console.WriteLine("Release error: {0}", error);
            //Console.ReadLine();
            /*Marshal.GetFunctionPointerForDelegate();
            var callbacks = new libspotifydotnet.libspotify.sp_session_callbacks {
                connection_error = IntPtr.Zero,
                connectionstate_updated = IntPtr.Zero,
                credentials_blob_updated = IntPtr.Zero,
                end_of_track = IntPtr.Zero,
                get_audio_buffer_stats = IntPtr.Zero,
                log_message = ,
                logged_in=,
                logged_out = ,
                message_to_user = ,
                metadata_updated = ,
                music_delivery = ,
                notify_main_thread = ,
                offline_error=,
                offline_status_updated = ,
                play_token_lost = ,
                private_session_mode_changed = ,
                scrobble_error = ,
                start_playback = ,
                stop_playback = ,
                streaming_error = ,
                userinfo_updated = 
            };*/


        }

        class Callbacks2 : SpotifySessionListener
        {
            ManualResetEvent iEv;

            public Callbacks2(ManualResetEvent aEv)
            {
                iEv = aEv;
            }

            public override void LoggedIn(SpotifySession session, SpotifyError error)
            {
                Console.WriteLine("logged_in(session, {0})", error);
            }
            public override void LoggedOut(SpotifySession session)
            {
                Console.WriteLine("logged_out(session)");
            }
            void DumpPlaylist(SpotifySession session, Playlist playlist)
            {
                int numTracks = playlist.NumTracks();
                for (int i = 0; i != numTracks; ++i)
                {
                    Track track = playlist.Track(i);
                    if (!track.IsLoaded())
                    {
                        Console.WriteLine("    Track #{0}: (not loaded)", i);
                        continue;
                    }
                    var availability = Track.GetAvailability(session, track);
                    Console.WriteLine("    Track #{0}: {1} [{2}]", i, FormatCSharpString(track.Name()), availability);
                }
            }
            public override void MetadataUpdated(SpotifySession session)
            {
                Console.WriteLine("metadata_updated(session)");
                var container = session.Playlistcontainer();
                if (!container.IsLoaded())
                {
                    Console.WriteLine("Container not loaded yet.");
                    return;
                }
                Console.WriteLine("PLAYLISTS:");
                int numLists = container.NumPlaylists();
                for (int i = 0; i != numLists; ++i)
                {
                    var playlistType = container.PlaylistType(i);
                    switch (playlistType)
                    {
                        case PlaylistType.Playlist:
                            var playlist = container.Playlist(i);
                            if (!playlist.IsLoaded())
                            {
                                Console.WriteLine("Playlist (not loaded)");
                                break;
                            }
                            Console.WriteLine("Playlist {0}", FormatCSharpString(playlist.Name()));
                            DumpPlaylist(session, playlist);
                            break;
                        case PlaylistType.StartFolder:
                        case PlaylistType.EndFolder:
                            string startEnd = playlistType == PlaylistType.StartFolder ? "start" : "end";
                            var folderName = container.PlaylistFolderName(i);
                            Console.WriteLine("Folder {0} {1}", startEnd, FormatCSharpString(folderName));
                            break;
                        case PlaylistType.Placeholder:
                            Console.WriteLine("Placeholder");
                            break;
                        default:
                            Console.WriteLine("Bad value");
                            break;
                    }
                }
                Console.WriteLine("END OF PLAYLISTS");
                Console.WriteLine("");
            }
            /*
            public void LoadTracks(IntPtr aSession)
            {
                var session = aSession;
                lock (iTracksToLoad)
                {
                    while (iTracksToLoad.Count > 0)
                    {
                        string trackUri = iTracksToLoad.Dequeue();
                        using (var utf8TrackUri = SpotifyMarshalling.StringToUtf8(trackUri))
                        {
                            IntPtr link = NativeMethods.sp_link_create_from_string(utf8TrackUri.IntPtr);
                            IntPtr track = NativeMethods.sp_link_as_track(link);
                            var loadError = NativeMethods.sp_session_player_load(session, track);
                            Console.WriteLine("Tried to load, got: {0}", loadError);
                            if (loadError == SpotifyError.Ok)
                            {
                                var playError = NativeMethods.sp_session_player_play(session, true);
                                Console.WriteLine("Tried to play, got: {0}", playError);
                            }
                        }
                    }
                }
            }*/
            public override void ConnectionError(SpotifySession session, SpotifyError error)
            {
                Console.WriteLine("connection_error(session, {0})", error);
            }
            public override void MessageToUser(SpotifySession session, string message)
            {
                Console.WriteLine("message_to_user(session, {0})", FormatCSharpString(message));
            }
            public override void NotifyMainThread(SpotifySession session)
            {
                Console.WriteLine("notify_main_thread(session)");
                iEv.Set();
            }
            public override int MusicDelivery(SpotifySession session, AudioFormat format, IntPtr frames, int num_frames)
            {
                Console.WriteLine("music(session, fmt={0}, frame_ptr={1}, count={2}, channels={3}, rate={4}, type={5})", format, frames, num_frames, format.channels, format.sample_rate, format.sample_type);
                return 0;
                /*if (format.sample_type != SampleType.Int16NativeEndian)
                {
                    return num_frames;
                }
                int bytes_per_frame = 2 * format.channels;
                int bytes_delivered = bytes_per_frame * num_frames;
                Console.WriteLine("Delivered {0} bytes", bytes_delivered);
                if (bytes_delivered > iTempBuffer.Length)
                {
                    num_frames = iTempBuffer.Length / bytes_per_frame;
                    bytes_delivered = bytes_per_frame * num_frames;
                }
                Marshal.Copy(frames, iTempBuffer, 0, bytes_delivered);
                Array.Copy(iTempBuffer, 0, iOutputBuffer, iOutputIndex, bytes_delivered);
                iOutputIndex += bytes_delivered;
                Console.WriteLine("    Now at index {0}", iOutputIndex);
                return num_frames;*/
            }
            public override void PlayTokenLost(SpotifySession session)
            {
                Console.WriteLine("play_token_lost(session)");
            }
            public override void LogMessage(SpotifySession session, string data)
            {
                Console.WriteLine("log_message(session, data={0})", FormatCSharpString(data));
            }
            public override void EndOfTrack(SpotifySession session)
            {
                Console.WriteLine("end_of_track");
            }
            public override void StreamingError(SpotifySession session, SpotifyError error)
            {
                Console.WriteLine("streaming_error(session, error={0})", error);
            }
            public override void UserinfoUpdated(SpotifySession session)
            {
                Console.WriteLine("userinfo_updated");
            }
            public override void StartPlayback(SpotifySession session)
            {
                Console.WriteLine("start_playback");
            }
            public override void StopPlayback(SpotifySession session)
            {
                Console.WriteLine("stop_playback");
            }
            public override void GetAudioBufferStats(SpotifySession session, out AudioBufferStats stats)
            {
                stats.stutter = 0;
                stats.samples = 4096;
                //Console.WriteLine("get_audio_buffer_stats");
            }
            public override void OfflineStatusUpdated(SpotifySession session)
            {
                Console.WriteLine("offline_status_updated");
            }
            public override void OfflineError(SpotifySession session, SpotifyError error)
            {
                Console.WriteLine("offline_error(session, error={0})", error);
            }
            public override void CredentialsBlobUpdated(SpotifySession session, string blob)
            {
                Console.WriteLine("credentials_blob_updated(session, blob={0})", FormatCSharpString(blob));
            }
            public override void ConnectionstateUpdated(SpotifySession session)
            {
                Console.WriteLine("connectionstate_updated");
            }
            public override void ScrobbleError(SpotifySession session, SpotifyError error)
            {
                Console.WriteLine("scrobble_error(session, error={0})", error);
            }
            public override void PrivateSessionModeChanged(SpotifySession session, bool is_private)
            {
                Console.WriteLine("private_session_mode_changed(session, is_private={0})", is_private);
            }
        }

        static void Main2(string[] args)
        {
            ManualResetEvent ev = new ManualResetEvent(false);
            CallbackAgent agent = new CallbackAgent(ev);
            byte[] appkey = File.ReadAllBytes("spotify_appkey.key");

            IntPtr appkeyPtr = Marshal.AllocHGlobal(appkey.Length);
            Marshal.Copy(appkey, 0, appkeyPtr, appkey.Length);

            var callbackStruct = agent.GetCallbacksStruct();
            IntPtr callbacksPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(sp_session_callbacks)));

            Marshal.StructureToPtr(callbackStruct, callbacksPtr, false);
            IntPtr session;
            SpotifyError error;
            using (var cache_location = new Utf8String("tmpdirabcd"))
            using (var settings_location = new Utf8String("settingsdirwxyz"))
            using (var user_agent = new Utf8String(".NET test program"))
            {
                var config = new sp_session_config
                {
                    api_version = 12,
                    application_key = appkeyPtr,
                    application_key_size = (UIntPtr)appkey.Length,
                    cache_location = cache_location.IntPtr,
                    callbacks = callbacksPtr,
                    compress_playlists = false,
                    dont_save_metadata_for_playlists = false,
                    initially_unload_playlists = false,
                    settings_location = settings_location.IntPtr,
                    user_agent = user_agent.IntPtr,
                };

                session = IntPtr.Zero;
                error = NativeMethods.sp_session_create(ref config, ref session);
            }
            Console.WriteLine("Create: {0}", error);
            Console.Write("username:");
            string username = Console.ReadLine();
            Console.Write("password:");
            string password = Console.ReadLine();

            using (var utf8username = new Utf8String(username))
            using (var utf8password = new Utf8String(password))
            {
                error = NativeMethods.sp_session_login(session, utf8username.IntPtr, utf8password.IntPtr, false, IntPtr.Zero);
            }
            Console.WriteLine("Login error: {0}", error);

            int timeout = 0;
            var consoleThread = new Thread(() =>
            {
                while (true)
                {
                    Console.WriteLine("Track URI:");
                    var uri = Console.ReadLine().Trim();
                    if (uri != "")
                    {
                        agent.QueueTrack(uri);
                    }
                    else
                    {
                        agent.Stop();
                    }
                }
            });
            consoleThread.Start();

            while (!agent.Finished)
            {
                ev.WaitOne(timeout);
                timeout = 0;
                while (timeout == 0)
                {
                    NativeMethods.sp_session_process_events(session, ref timeout);
                }
                agent.LoadTracks(session);

            }

            var pcmData = agent.Buffer;

            using (var f = File.OpenWrite("soundout.wav"))
            using (var writer = new BinaryWriter(f))
            {
                WaveWriter.WriteWave(writer, 41000, 2, 2, pcmData);
            }
            File.WriteAllBytes("rawpcm", agent.Buffer);
            error = NativeMethods.sp_session_player_unload(session);
            Console.WriteLine("unload: {0}", error);
            error = NativeMethods.sp_session_release(session);
            Console.WriteLine("release: {0}", error);

            //Console.ReadLine();
            // Skip shutdown - libspotify.NET is a mess. 
            //error = libspotify.sp_session_release(ref session);
            //Console.WriteLine("Release error: {0}", error);
            //Console.ReadLine();
            /*Marshal.GetFunctionPointerForDelegate();
            var callbacks = new libspotifydotnet.libspotify.sp_session_callbacks {
                connection_error = IntPtr.Zero,
                connectionstate_updated = IntPtr.Zero,
                credentials_blob_updated = IntPtr.Zero,
                end_of_track = IntPtr.Zero,
                get_audio_buffer_stats = IntPtr.Zero,
                log_message = ,
                logged_in=,
                logged_out = ,
                message_to_user = ,
                metadata_updated = ,
                music_delivery = ,
                notify_main_thread = ,
                offline_error=,
                offline_status_updated = ,
                play_token_lost = ,
                private_session_mode_changed = ,
                scrobble_error = ,
                start_playback = ,
                stop_playback = ,
                streaming_error = ,
                userinfo_updated = 
            };*/


        }
    }
}
