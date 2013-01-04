using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpotifySharp
{
    public partial class Album
    {
        public ImageId Cover(ImageSize size)
        {
            return new ImageId(NativeMethods.sp_album_cover(this._handle, size));
        }
    }
}
