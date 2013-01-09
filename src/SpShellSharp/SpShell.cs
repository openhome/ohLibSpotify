﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SpotifySharp;

namespace SpShellSharp
{
    interface IMetadataWaiter
    {
        void AddMetadataUpdatedCallback(Action aCallback);
        void RemoveMetadataUpdatedCallback(Action aCallback);
    }

    interface IConsoleReader
    {
        void RequestInput(string aPrompt);
    }

    class SpShell : SpotifySessionListener, IDisposable, IMetadataWaiter
    {
        AutoResetEvent iSpotifyEvent;
        SpotifySession iSession;
        ConsoleReader iReader;
        bool iLogToStderr;
        ConsoleCommandDictionary iCommands;
        List<Action> iMetadataUpdateActions = new List<Action>();
        Browser iBrowser;
        Action iMetadataUpdatedCallbacks;

        public SpShell(AutoResetEvent aSpotifyEvent, string aUsername, string aPassword, string aBlob, bool aSelftest, ConsoleReader aReader)
        {
            iReader = aReader;
            iSpotifyEvent = aSpotifyEvent;
            byte[] appkey = File.ReadAllBytes("spotify_appkey.key");
            SpotifySessionConfig config = new SpotifySessionConfig();
            config.ApiVersion = 12;
            config.CacheLocation = aSelftest ? "" : "tmp";
            config.SettingsLocation = aSelftest ? "" : "tmp";
            config.ApplicationKey = appkey;
            config.UserAgent = "spshell#";
            config.Listener = this;

            try
            {
                iSession = SpotifySession.Create(config);
            }
            catch (SpotifyException e)
            {
                Console.Error.WriteLine("Failed to create session: {0}", e.Message);
                throw;
            }

            iBrowser = new Browser(iSession, this, aReader);

            iCommands = new ConsoleCommandDictionary(CmdDone)
                        {
                            { "log",      CmdLog,              "Enable/Disable logging to console (default off)" },
                            { "logout",   CmdLogout,           "Logout and exit app" },
                            { "exit",     CmdLogout,           "Logout and exit app" },
                            { "quit",     CmdLogout,           "Logout and exit app" },
                            { "browse",   iBrowser.CmdBrowse,  "Browse a Spotify URL" },
                        };
            iCommands.Add("help", iCommands.CmdHelp, "This help");

            try
            {
                if (aUsername == null)
                {
                    iSession.Relogin();
                    var reloginname = iSession.RememberedUser();
                    Console.Error.WriteLine("Trying to relogin as user {0}", reloginname);
                }
                else
                {
                    iSession.Login(aUsername, aPassword, true, aBlob);
                }
            }
            catch (SpotifyException e)
            {
                if (e.Error == SpotifyError.NoCredentials)
                {
                    Console.Error.WriteLine("No stored credentials");
                    throw;
                }
            }
        }

        public void ProcessEvents(ref int aNextTimeout)
        {
            iSession.ProcessEvents(ref aNextTimeout);
        }

        public bool IsFinished
        {
            get; private set;
        }

        public override void NotifyMainThread(SpotifySession session)
        {
            iSpotifyEvent.Set();
        }

        public void ProcessConsoleInput(string aInput)
        {
            iCommands.Execute(aInput);
        }

        public void Dispose()
        {
            if (iSession != null)
            {
                iSession.Dispose();
            }
            iSession = null;
        }

        public override void ConnectionError(SpotifySession session, SpotifyError error)
        {
            Console.Error.WriteLine("Connection to Spotify failed: {0}", Spotify.ErrorMessage(error));
        }

        public override void LoggedIn(SpotifySession session, SpotifyError error)
        {
            if (error != SpotifyError.Ok)
            {
                IsFinished = true;
                Console.WriteLine("Failed to log in to Spotify: {0}", Spotify.ErrorMessage(error));
            }
            var me = session.User();
            string displayName = me.IsLoaded() ? me.DisplayName() : me.CanonicalName();
            string username = session.UserName();
            var cc = session.UserCountry();
            Console.Error.WriteLine("Logged in to Spotify as user {0} [{1}] (registered in country: {2})", username, displayName, Spotify.CountryString(cc));
            iReader.RequestInput("> ");
            // TODO: self test
        }
        public override void LoggedOut(SpotifySession session)
        {
            IsFinished = true;
        }
        public override void ScrobbleError(SpotifySession session, SpotifyError error)
        {
            Console.Error.WriteLine("Scrobble failure: {0}", Spotify.ErrorMessage(error));
        }
        public override void PrivateSessionModeChanged(SpotifySession session, bool is_private)
        {
            Console.WriteLine("private session mode changed: {0}", is_private);
        }
        public override void CredentialsBlobUpdated(SpotifySession session, string blob)
        {
            Console.WriteLine("blob for storage: {0}", blob);
        }
        public override void LogMessage(SpotifySession session, string data)
        {
            if (iLogToStderr)
            {
                Console.Error.WriteLine(data);
            }
        }
        public override void OfflineStatusUpdated(SpotifySession session)
        {
            OfflineSyncStatus status = new OfflineSyncStatus();
            session.OfflineSyncGetStatus(ref status);
            if (status.syncing)
            {
                Console.WriteLine("Offline status: queued:{0}:{1} done:{2}:{3} copied:{4}:{5} nocopy:{6} err:{7}",
                    status.queued_tracks,
                    status.queued_bytes,
                    status.done_tracks,
                    status.done_bytes,
                    status.copied_tracks,
                    status.copied_bytes,
                    status.willnotcopy_tracks,
                    status.error_tracks);
            }
            else
            {
                Console.WriteLine("Offline status: Idle");
            }
        }
        public override void MetadataUpdated(SpotifySession session)
        {
            Action callbacks = iMetadataUpdatedCallbacks;
            if (callbacks != null)
            {
                callbacks();
            }
        }
        public int CmdLog(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("log enable|disable");
                return -1;
            }
            iLogToStderr = args[1] == "enable";
            return 1;
        }
        public int CmdLogout(string[] args)
        {
            if (args.Length == 2 && args[1] == "permanent")
            {
                Console.Error.WriteLine("Dropping stored credentials");
                iSession.ForgetMe();
            }
            iSession.Logout();
            return 0;
        }
        public void CmdDone()
        {
            iReader.RequestInput("> ");
        }


        public void AddMetadataUpdatedCallback(Action aAction)
        {
            iMetadataUpdatedCallbacks += aAction;
        }

        public void RemoveMetadataUpdatedCallback(Action aAction)
        {
            iMetadataUpdatedCallbacks -= aAction;
        }
    }
}