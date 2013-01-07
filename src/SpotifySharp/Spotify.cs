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
    }
}
