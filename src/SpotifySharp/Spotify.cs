using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpotifySharp
{
    public static class Spotify
    {
        public static string BuildId()
        {
            return SpotifyMarshalling.Utf8ToString(NativeMethods.sp_build_id());
        }
        public static string ErrorMessage(SpotifyError error)
        {
            return SpotifyMarshalling.Utf8ToString(NativeMethods.sp_error_message(error));
        }
        public static string CountryString(int country)
        {
            return "" + (char)(country >> 8) + (char)(country & 0xff);
        }
        internal const string NativeLibrary = "libspotify";
    }
}
