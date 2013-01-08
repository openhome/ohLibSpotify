using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SpotifySharp;

namespace SpShellSharp
{
    class ConsoleCommand
    {
        public Func<string[], int> Function { get; private set; }
        public string HelpText { get; private set; }

        public ConsoleCommand(Func<string[], int> aFunction, string aHelpText)
        {
            Function = aFunction;
            HelpText = aHelpText;
        }
    }

    class ConsoleCommandDictionary : IEnumerable<ConsoleCommand>
    {
        Dictionary<string, ConsoleCommand> iCommands = new Dictionary<string, ConsoleCommand>();
        List<string> iOrder = new List<string>();
        Action iDone;

        public ConsoleCommandDictionary(Action aDone)
        {
            iDone = aDone;
        }

        public void Add(string aName, Func<string[], int> aFunction, string aHelpText)
        {
            iCommands.Add(aName, new ConsoleCommand(aFunction, aHelpText));
            iOrder.Add(aName);
        }

        public IEnumerator<ConsoleCommand> GetEnumerator()
        {
            return iCommands.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Execute(string aCommand)
        {
            string[] args = aCommand.Split();
            Execute(args);
        }

        public void Execute(string[] aCommand)
        {
            if (aCommand.Length == 0 || aCommand[0].Trim()=="")
            {
                iDone();
                return;
            }
            ConsoleCommand selectedCommand;
            if (iCommands.TryGetValue(aCommand[0], out selectedCommand))
            {
                if (selectedCommand.Function(aCommand) != 0)
                {
                    iDone();
                }
                return;
            }
            Console.WriteLine("No such command");
            iDone();
        }

        public int CmdHelp(string[] aCommand)
        {
            foreach (string name in iOrder)
            {
                Console.WriteLine("  {0,-20} {1}", name, iCommands[name].HelpText);
            }
            return -1;
        }
    }

    class SpShell : SpotifySessionListener, IDisposable
    {
        AutoResetEvent iSpotifyEvent;
        SpotifySession iSession;
        ConsoleReader iReader;
        bool iLogToStderr;
        ConsoleCommandDictionary iCommands;

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

            iCommands = new ConsoleCommandDictionary(CmdDone)
            {
                { "logout", CmdLogout,  "Logout and exit app" },
                { "exit",   CmdLogout,  "Logout and exit app" },
                { "quit",   CmdLogout,  "Logout and exit app" }
            };
            iCommands.Add("help", iCommands.CmdHelp, "This help");

            try
            {
                iSession = SpotifySession.Create(config);
            }
            catch (SpotifyException e)
            {
                Console.Error.WriteLine("Failed to create session: {0}", e.Message);
                throw;
            }

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


    }

    class ConsoleReader : IDisposable
    {
        AutoResetEvent iRequestInput;
        AutoResetEvent iProvideInput;
        string iInput;
        bool iStop;
        Thread iThread;

        public WaitHandle InputReady { get { return iProvideInput; } }

        public ConsoleReader()
        {
            iRequestInput = new AutoResetEvent(false);
            iProvideInput = new AutoResetEvent(false);
            iThread = new Thread(Run);
            iThread.IsBackground = true;
            iThread.Start();
        }
        public string GetInput()
        {
            return iInput;
        }
        public void RequestInput(string aPrompt)
        {
            Console.Write(aPrompt);
            iRequestInput.Set();
        }
        public void Stop()
        {
            iStop = true;
            iRequestInput.Set();
        }

        public void Run()
        {
            while (true)
            {
                iRequestInput.WaitOne();
                if (iStop) return;
                iInput = Console.ReadLine();
                iProvideInput.Set();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string username = args.Length > 0 ? args[0] : null;
            string blob = args.Length > 1 ? args[1] : null;
            string password = null;
            bool selftest = args.Length > 2 ? args[2] == "selftest" : false;

            using (var consoleReader = new ConsoleReader())
            {


                WaitHandle[] handles = new WaitHandle[2];
                AutoResetEvent spotifyEvent;
                handles[0] = spotifyEvent = new AutoResetEvent(false);
                handles[1] = consoleReader.InputReady;

                Console.WriteLine("Using libspotify {0}", Spotify.BuildId());

                if (username == null)
                {
                    Console.Write("Username (press enter to login with stored credentials): ");
                    username = (Console.ReadLine() ?? "").TrimEnd();
                    if (username == "") username = null;
                }

                if (username != null && blob == null)
                {
                    Console.WriteLine("Password: ");
                    // No easy cross-platform way to turn off console echo.
                    // Password will be visible!
                    password = (Console.ReadLine() ?? "").TrimEnd();
                }

                using (SpShell shell = new SpShell(spotifyEvent, username, password, blob, selftest, consoleReader))
                {
                    //consoleReader.RequestInput("> ");
                    int next_timeout = 0;
                    while (!shell.IsFinished)
                    {
                        int ev = WaitHandle.WaitAny(handles, next_timeout != 0 ? next_timeout : Timeout.Infinite);
                        switch (ev)
                        {
                            case 0:
                            case WaitHandle.WaitTimeout:
                                do
                                {
                                    shell.ProcessEvents(ref next_timeout);
                                } while (next_timeout == 0);
                                if (selftest)
                                {
                                    // TODO: TestProcess
                                }
                                break;
                            case 1:
                                shell.ProcessConsoleInput(consoleReader.GetInput());
                                break;
                        }
                    }
                    Console.WriteLine("Logged out");
                }
                Console.WriteLine("Exiting...");
            }
        }
    }
}
