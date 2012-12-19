using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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


    internal class Utf8String : IDisposable
    {
        IntPtr iPtr;
        public IntPtr IntPtr { get { return iPtr; } }
        public int BufferLength { get { return iBufferSize; } }
        int iBufferSize;
        public Utf8String(int aBufferSize)
        {
            if (aBufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("aBufferSize", "Argument must be positive.");
            }
            iPtr = Marshal.AllocHGlobal(aBufferSize);
            iBufferSize = aBufferSize;
        }
        public Utf8String(string aValue)
        {
            if (aValue == null)
            {
                iPtr = IntPtr.Zero;
            }
            else
            {
                byte[] bytes = Encoding.UTF8.GetBytes(aValue);
                iPtr = Marshal.AllocHGlobal(bytes.Length + 1);
                Marshal.Copy(bytes, 0, iPtr, bytes.Length);
                Marshal.WriteByte(iPtr, bytes.Length, 0);
                iBufferSize = bytes.Length + 1;
            }
        }
        public void ReallocIfSmaller(int aMinLength)
        {
            if (iPtr == IntPtr.Zero)
            {
                throw new ObjectDisposedException("Utf8String");
            }
            if (iBufferSize <= aMinLength)
            {
                iPtr = Marshal.ReAllocHGlobal(iPtr, (IntPtr)aMinLength);
                iBufferSize = aMinLength;
            }
        }
        public string Value
        {
            get
            {
                if (iPtr == IntPtr.Zero)
                {
                    throw new ObjectDisposedException("Utf8String");
                }
                return SpotifyMarshalling.Utf8ToString(iPtr);
            }
        }
        public void Dispose()
        {
            if (iPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(iPtr);
                iPtr = IntPtr.Zero;
            }
        }

        public string GetString(int aStringLengthBuffer)
        {
            return Value; // TODO: Include \0 characters.
        }
    }
    internal static class SpotifyMarshalling
    {
        // This represents the sp_subscribers struct in libspotify.
        //
        // It looks roughly like this:
        // struct
        // {
        //     int count;
        //     char *subscribers[1];
        // }
        //
        // In actual fact, the array might have variable length, specified
        // by count.
        struct SpotifySubscribers
        {
            // Disable warnings about unassigned fields. We don't even instantiate
            // this class. It only exists for marshalling calculations.
#pragma warning disable 649
            // The size of the array.
            public int Count;
            // The first (index 0) item in the array.
            public IntPtr FirstSubscriber;
#pragma warning restore 649
        }

        public static string[] SubscribersToStrings(IntPtr aSubscribers)
        {
            // This is pretty painful.
            // Assumptions
            //     * C int is 32-bit. (This assumption is pervasive in P/Invoke code and
            //       not portable, e.g. to ILP64 systems, but it holds for every current
            //       system we might care about: Windows/Linux/Mac x86/x64, iOs/Android/
            //       Linux arm.)
            //     * Structs may not have padding before the first element. (Guaranteed by C.)
            //     * Arrays may not have padding before the first element. (Guaranteed by C?)
            //     * An array of pointers has the same alignment requirement as a single pointer. (?)
            // First of all, find the offset of the first item of the array inside the structure.
            var structOffset = (int)Marshal.OffsetOf(typeof(SpotifySubscribers), "FirstSubscriber");

            // Construct a pointer to the first item of the array.
            var arrayPtr = aSubscribers + structOffset;

            // Extract Count. I'm not 100% it's safe to use Marshal.PtrToStructure here, so
            // I'm using Marshal.Copy to extract the count into an array.
            int[] countArray = new int[1];
            Marshal.Copy(aSubscribers, countArray, 0, 1);
            int count = countArray[0];

            // Copy the array content into a managed array.
            IntPtr[] utf8Strings = new IntPtr[count];
            Marshal.Copy(arrayPtr, utf8Strings, 0, count);

            // Finally convert the strings to managed strings.
            return utf8Strings.Select(Utf8ToString).ToArray();
        }

        public static Utf8String StringToUtf8(string aString)
        {
            return new Utf8String(aString);
        }
        public static Utf8String AllocBuffer(int aBufferSize)
        {
            return new Utf8String(aBufferSize);
        }
        public static string Utf8ToString(IntPtr aUtf8)
        {
            if (aUtf8 == IntPtr.Zero)
                return null;
            int len = 0;
            while (Marshal.ReadByte(aUtf8, len) != 0)
                len++;
            if (len == 0)
                return "";
            byte[] array = new byte[len];
            Marshal.Copy(aUtf8, array, 0, len);
            return Encoding.UTF8.GetString(array);
        }

        public static void CheckError(SpotifyError aError)
        {
            if (aError == SpotifyError.Ok) return;
            string message = Utf8ToString(NativeMethods.sp_error_message(aError));
            throw new SpotifyException(aError, message);
        }


        static Dictionary<IntPtr, SpotifySession> iSpotifySessions = new Dictionary<IntPtr, SpotifySession>();
        static Dictionary<Tuple<IntPtr, IntPtr>, object> iCallbackObjects = new Dictionary<Tuple<IntPtr, IntPtr>, object>();
        static object iGlobalLock = new object();

        public static SpotifySession GetManagedSession(IntPtr aSessionPtr)
        {
            lock (iGlobalLock)
            {
                SpotifySession session;
                if (iSpotifySessions.TryGetValue(aSessionPtr, out session))
                {
                    return session;
                }
                session = new SpotifySession(aSessionPtr);
                iSpotifySessions[aSessionPtr] = session;
                return session;
            }
        }

        public static void ReleaseManagedSession(IntPtr aSessionPtr)
        {
            lock (iGlobalLock)
            {
                iSpotifySessions.Remove(aSessionPtr);
            }
        }

        public static object GetCallbackObject(IntPtr aSpotifyObject, IntPtr aUserData)
        {
            lock (iGlobalLock)
            {
                object obj;
                if (!iCallbackObjects.TryGetValue(Tuple.Create(aSpotifyObject, aUserData), out obj))
                {
                    Console.WriteLine("No such spotify object: {0}", aSpotifyObject);
                    return null;
                    //throw new Exception("Spotify callback occurred after callbacks were unregistered.");
                }
                return obj;
            }
        }

        public static void RegisterCallbackObject(IntPtr aSpotifyObject, IntPtr aUserData, object aCallbackObject)
        {
            // Note: There might appear to be a race, in that CreateCallbackObject can
            // only be invoked after 
            lock (iGlobalLock)
            {
                Console.WriteLine("Registered spotify object: {0}", aSpotifyObject);
                var key = Tuple.Create(aSpotifyObject, aUserData);
                if (iCallbackObjects.ContainsKey(key))
                {
                    throw new Exception("Spotify callback occurred after callbacks were unregistered.");
                }
                iCallbackObjects.Add(key, aCallbackObject);
            }
        }

        public static void UnregisterCallbackObject(IntPtr aSpotifyObject, IntPtr aUserData)
        {
            lock (iGlobalLock)
            {
                var key = Tuple.Create(aSpotifyObject, aUserData);
                iCallbackObjects.Remove(key);
            }
        }
    }

    [Serializable]
    public class SpotifyException : Exception
    {
        public SpotifyError Error { get; private set; }
        public SpotifyException(SpotifyError error)
        {
            Error = error;
        }
        public SpotifyException(SpotifyError error, string message) : base(message)
        {
            Error = error;
        }
        protected SpotifyException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }


    static class SessionDelegates
    {
        public static readonly sp_session_callbacks SessionCallbacks = CreateSessionCallbacks();
        static sp_session_callbacks CreateSessionCallbacks()
        {
            return new sp_session_callbacks
            {
                logged_in = logged_in,
                logged_out = logged_out,
                metadata_updated = metadata_updated,
                connection_error = connection_error,
                message_to_user = MessageToUser,
                notify_main_thread = NotifyMainThread,
                music_delivery = MusicDelivery,
                play_token_lost = PlayTokenLost,
                log_message = LogMessage,
                end_of_track = EndOfTrack,
                streaming_error = StreamingError,
                userinfo_updated = UserinfoUpdated,
                start_playback = StartPlayback,
                stop_playback = StopPlayback,
                get_audio_buffer_stats = GetAudioBufferStats,
                offline_status_updated = OfflineStatusUpdated,
                offline_error = OfflineError,
                credentials_blob_updated = CredentialsBlobUpdated,
                connectionstate_updated = ConnectionstateUpdated,
                scrobble_error = ScrobbleError,
                private_session_mode_changed = PrivateSessionModeChanged
            };
        }

        struct SessionAndListener
        {
            public SpotifySession Session;
            public SpotifySessionListener Listener;
            public bool IsReady;
        }
        static SessionAndListener GetListener(IntPtr nativeSession)
        {
            SessionAndListener retVal = new SessionAndListener();
            retVal.Session = SpotifyMarshalling.GetManagedSession(nativeSession);
            retVal.Listener = retVal.Session != null ? retVal.Session.Listener : null;
            retVal.IsReady = retVal.Listener != null;
            return retVal;
        }
        
        static void logged_in(IntPtr @session, SpotifyError @error)
        {
            var context = GetListener(session);
            context.Listener.LoggedIn(context.Session, error);
        }
        static void logged_out(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.LoggedOut(context.Session);
        }
        static void metadata_updated(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.MetadataUpdated(context.Session);
        }
        static void connection_error(IntPtr @session, SpotifyError @error)
        {
            var context = GetListener(session);
            context.Listener.ConnectionError(context.Session,error);
        }
        static void MessageToUser(IntPtr @session, IntPtr @message)
        {
            var context = GetListener(session);
            context.Listener.MessageToUser(context.Session,SpotifyMarshalling.Utf8ToString(message));
        }
        static void NotifyMainThread(IntPtr @session)
        {
            // notify_main_thread can (and *does*) occur on another thread before
            // we've even returned from sp_session_create and taken note of our session
            // pointer to associate it with our managed listener. If that happens, the
            // notification goes to the NullSessionListener instance that the SpotifSession
            // was constructed with and is discarded. A better solution might be to record
            // the notification and deliver it when the true Listener is finally associated
            // with the SpotifySession.
            // Even better would be to use sp_session_userdata...
            var context = GetListener(session);
            context.Listener.NotifyMainThread(context.Session);
        }
        static int MusicDelivery(IntPtr @session, ref AudioFormat @format, IntPtr @frames, int @num_frames)
        {
            var context = GetListener(session);
            return context.Listener.MusicDelivery(context.Session,format, frames, num_frames);
        }
        static void PlayTokenLost(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.PlayTokenLost(context.Session);
        }
        static void LogMessage(IntPtr @session, IntPtr @data)
        {
            var context = GetListener(session);
            context.Listener.LogMessage(context.Session,SpotifyMarshalling.Utf8ToString(data));
        }
        static void EndOfTrack(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.EndOfTrack(context.Session);
        }
        static void StreamingError(IntPtr @session, SpotifyError @error)
        {
            var context = GetListener(session);
            context.Listener.StreamingError(context.Session,error);
        }
        static void UserinfoUpdated(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.UserinfoUpdated(context.Session);
        }
        static void StartPlayback(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.StartPlayback(context.Session);
        }
        static void StopPlayback(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.StopPlayback(context.Session);
        }
        static void GetAudioBufferStats(IntPtr @session, ref AudioBufferStats @stats)
        {
            var context = GetListener(session);
            context.Listener.GetAudioBufferStats(context.Session,out stats);
        }
        static void OfflineStatusUpdated(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.OfflineStatusUpdated(context.Session);
        }
        static void OfflineError(IntPtr @session, SpotifyError @error)
        {
            var context = GetListener(session);
            context.Listener.OfflineError(context.Session,error);
        }
        static void CredentialsBlobUpdated(IntPtr @session, IntPtr @blob)
        {
            var context = GetListener(session);
            context.Listener.CredentialsBlobUpdated(context.Session,SpotifyMarshalling.Utf8ToString(blob));
        }
        static void ConnectionstateUpdated(IntPtr @session)
        {
            var context = GetListener(session);
            context.Listener.ConnectionstateUpdated(context.Session);
        }
        static void ScrobbleError(IntPtr @session, SpotifyError @error)
        {
            var context = GetListener(session);
            context.Listener.ScrobbleError(context.Session,error);
        }
        static void PrivateSessionModeChanged(IntPtr @session, [MarshalAs(UnmanagedType.I1)]bool @is_private)
        {
            var context = GetListener(session);
            context.Listener.PrivateSessionModeChanged(context.Session, is_private);
        }
    }

    public abstract class SpotifySessionListener
    {
        public virtual void LoggedIn(SpotifySession @session, SpotifyError @error) { }
        public virtual void LoggedOut(SpotifySession @session) { }
        public virtual void MetadataUpdated(SpotifySession @session) { }
        public virtual void ConnectionError(SpotifySession @session, SpotifyError @error) { }
        public virtual void MessageToUser(SpotifySession @session, string @message) { }
        public virtual void NotifyMainThread(SpotifySession @session) { }
        public virtual int MusicDelivery(SpotifySession @session, AudioFormat @format, IntPtr @frames, int @num_frames)
        {
            return 0;
        }
        public virtual void PlayTokenLost(SpotifySession @session) { }
        public virtual void LogMessage(SpotifySession @session, string @data) { }
        public virtual void EndOfTrack(SpotifySession @session) { }
        public virtual void StreamingError(SpotifySession @session, SpotifyError @error) { }
        public virtual void UserinfoUpdated(SpotifySession @session) { }
        public virtual void StartPlayback(SpotifySession @session) { }
        public virtual void StopPlayback(SpotifySession @session) { }
        public virtual void GetAudioBufferStats(SpotifySession @session, out AudioBufferStats @stats)
        {
            stats.samples = 0;
            stats.stutter = 0;
        }
        public virtual void OfflineStatusUpdated(SpotifySession @session) { }
        public virtual void OfflineError(SpotifySession @session, SpotifyError @error) { }
        public virtual void CredentialsBlobUpdated(SpotifySession @session, string @blob) { }
        public virtual void ConnectionstateUpdated(SpotifySession @session) { }
        public virtual void ScrobbleError(SpotifySession @session, SpotifyError @error) { }
        public virtual void PrivateSessionModeChanged(SpotifySession @session, bool @is_private) { }
    }

    public sealed class NullSessionListener : SpotifySessionListener
    {
        static SpotifySessionListener iInstance = new NullSessionListener();
        public static SpotifySessionListener Instance { get { return iInstance; } }
    }

    /*
    internal struct sp_session_config
    {
        public int @api_version;
        public IntPtr @cache_location;
        public IntPtr @settings_location;
        public IntPtr @application_key;
        public UIntPtr @application_key_size;
        public IntPtr @user_agent;
        public IntPtr @callbacks;
        public IntPtr @userdata;
        [MarshalAs(UnmanagedType.I1)]
        public bool @compress_playlists;
        [MarshalAs(UnmanagedType.I1)]
        public bool @dont_save_metadata_for_playlists;
        [MarshalAs(UnmanagedType.I1)]
        public bool @initially_unload_playlists;
        public IntPtr @device_id;
        public IntPtr @proxy;
        public IntPtr @proxy_username;
        public IntPtr @proxy_password;
        public IntPtr @ca_certs_filename;
        public IntPtr @tracefile;
    }

    internal struct sp_playlist_callbacks
    {
        public tracks_added @tracks_added;
        public tracks_removed @tracks_removed;
        public tracks_moved @tracks_moved;
        public playlist_renamed @playlist_renamed;
        public playlist_state_changed @playlist_state_changed;
        public playlist_update_in_progress @playlist_update_in_progress;
        public playlist_metadata_updated @playlist_metadata_updated;
        public track_created_changed @track_created_changed;
        public track_seen_changed @track_seen_changed;
        public description_changed @description_changed;
        public image_changed @image_changed;
        public track_message_changed @track_message_changed;
        public subscribers_changed @subscribers_changed;
    }

    internal struct sp_playlistcontainer_callbacks
    {
        public playlist_added @playlist_added;
        public playlist_removed @playlist_removed;
        public playlist_moved @playlist_moved;
        public container_loaded @container_loaded;
    }*/

    /*
    public partial class SpotifySession
    {
        IntPtr _handle;
        internal SpotifySession(IntPtr handle) { _handle = handle; }
        ~SpotifySession()
        {
            Dispose(false);
        }
        void Release()
        {
            NativeMethods.sp_session_release(_handle);
        }
        void CheckDisposed()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException("SpotifySession");
            }
        }
        void Dispose(bool disposing)
        {
            if (_handle != IntPtr.Zero)
            {
                Release();
                _handle = IntPtr.Zero;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }*/
/*
    public partial class SpotifyPlaylist
    {
        IntPtr _handle;
        internal SpotifyPlaylist(IntPtr handle) { _handle = handle; }
        private ~SpotifyPlaylist()
        {
            Dispose(false);
        }
        void AddRef()
        {
            NativeMethods.sp_playlist_add_ref(_handle);
        }
        void Release()
        {
            NativeMethods.sp_playlist_release(_handle);
        }
        void CheckDisposed()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException("SpotifyPlaylist");
            }
        }
        void Dispose(bool disposing)
        {
            if (_handle != IntPtr.Zero)
            {
                Release();
                _handle = IntPtr.Zero;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }



        public void Rename(string new_name)
        {
            using (var utf8_new_name = SpotifyMarshalling.StringToUtf8(new_name))
            {
                NativeMethods.sp_playlist_
                var error = NativeMethods.sp_playlist_rename(_handle, utf8_new_name.IntPtr);

                SpotifyMarshalling.CheckError(error);
            }
        }
    }
 * */

    public class SpotifySessionConfig
    {
        public int ApiVersion { get; set; }
        public string CacheLocation { get; set; }
        public string SettingsLocation { get; set; }
        public byte[] ApplicationKey { get; set; }
        public string UserAgent { get; set; }
        public SpotifySessionListener Listener { get; set; }
        public IntPtr UserData { get; set; }
        //public IntPtr @userdata;
        public bool CompressPlaylists { get; set; }
        public bool DontSaveMetadataForPlaylists { get; set; }
        public bool InitiallyUnloadPlaylists { get; set; }
        public string DeviceId { get; set; }
        public string Proxy { get; set; }
        public string ProxyUsername { get; set; }
        public string ProxyPassword { get; set; }
        public string CACertsFilename { get; set; }
        public string TraceFile { get; set; }
    }
    /*
    public class WeakSpotifySession : SpotifySession {
        internal WeakSpotifySession(IntPtr handle) : base(handle)
        {
        }
    }
    public class DisposableSpotifySession : SpotifySession, IDisposable
    {
        internal DisposableSpotifySession(IntPtr handle) : base(handle)
        {
        }

        public void Dispose()
        {
            SpotifyMarshalling.RegisterCallbackObject(
            throw new NotImplementedException();
        }
    }*/
    public sealed partial class SpotifySession : IDisposable
    {
        SpotifySessionListener iListener = new NullSessionListener();
        internal SpotifySessionListener Listener
        {
            get { return iListener; }
            set { iListener = value; }
        }


        public static SpotifySession Create(SpotifySessionConfig config)
        {
            IntPtr sessionPtr = IntPtr.Zero;
            using (var cacheLocation = SpotifyMarshalling.StringToUtf8(config.CacheLocation))
            using (var settingsLocation = SpotifyMarshalling.StringToUtf8(config.SettingsLocation))
            using (var userAgent = SpotifyMarshalling.StringToUtf8(config.UserAgent))
            using (var deviceId = SpotifyMarshalling.StringToUtf8(config.DeviceId))
            using (var proxy = SpotifyMarshalling.StringToUtf8(config.Proxy))
            using (var proxyUsername = SpotifyMarshalling.StringToUtf8(config.ProxyUsername))
            using (var proxyPassword = SpotifyMarshalling.StringToUtf8(config.ProxyPassword))
            using (var caCertsFilename = SpotifyMarshalling.StringToUtf8(config.CACertsFilename))
            using (var traceFile = SpotifyMarshalling.StringToUtf8(config.TraceFile))
            {
                IntPtr appKeyPtr = IntPtr.Zero;
                IntPtr callbacksPtr = IntPtr.Zero;
                try
                {
                    byte[] appkey = config.ApplicationKey;
                    appKeyPtr = Marshal.AllocHGlobal(appkey.Length);
                    Marshal.Copy(config.ApplicationKey, 0, appKeyPtr, appkey.Length);
                    callbacksPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(sp_session_callbacks)));
                    Marshal.StructureToPtr(SessionDelegates.SessionCallbacks, callbacksPtr, false);
                    sp_session_config nativeConfig = new sp_session_config {
                        api_version = config.ApiVersion,
                        cache_location = cacheLocation.IntPtr,
                        settings_location = settingsLocation.IntPtr,
                        application_key = appKeyPtr,
                        application_key_size = (UIntPtr)appkey.Length,
                        user_agent = userAgent.IntPtr,
                        callbacks = callbacksPtr,
                        userdata = config.UserData,
                        compress_playlists = config.CompressPlaylists,
                        dont_save_metadata_for_playlists = config.DontSaveMetadataForPlaylists,
                        initially_unload_playlists = config.InitiallyUnloadPlaylists,
                        device_id = deviceId.IntPtr,
                        proxy = proxy.IntPtr,
                        proxy_username = proxyUsername.IntPtr,
                        proxy_password = proxyPassword.IntPtr,
                        ca_certs_filename = caCertsFilename.IntPtr,
                        tracefile = traceFile.IntPtr,
                    };
                    var error = NativeMethods.sp_session_create(ref nativeConfig, ref sessionPtr);
                    SpotifyMarshalling.CheckError(error);
                    SpotifyMarshalling.RegisterCallbackObject(sessionPtr, IntPtr.Zero, config.Listener);
                }
                finally
                {
                    if (appKeyPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(appKeyPtr);
                    }
                    if (callbacksPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(callbacksPtr);
                    }
                }
            }
            SpotifySession session = SpotifyMarshalling.GetManagedSession(sessionPtr);
            session.Listener = config.Listener;
            return session;
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            var error = NativeMethods.sp_session_release(_handle);
            SpotifyMarshalling.ReleaseManagedSession(_handle);
            _handle = IntPtr.Zero;
            SpotifyMarshalling.CheckError(error);
        }
    }
}
