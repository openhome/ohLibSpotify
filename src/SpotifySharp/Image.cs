﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace SpotifySharp
{
    public sealed partial class Image
    {

        internal struct ListenerAndUserdata
        {
            public ImageLoaded Listener;
            public object Userdata;
        }
        internal static readonly UserDataTable<ImageLoaded> ListenerTable = new UserDataTable<ImageLoaded>();
        public void AddLoadCallbacks(ImageLoaded listener, object userdata)
        {
            IntPtr nativeUserdata = ListenerTable.PutListener(this._handle, listener, userdata);
            NativeMethods.sp_image_add_load_callback(this._handle, ImageDelegates.Callback, nativeUserdata);
        }
        public void RemoveLoadCallback(object userdata)
        {
            IntPtr nativeUserdata;
            if (!ListenerTable.TryGetNativeUserdata(this._handle, userdata, out nativeUserdata))
            {
                throw new ArgumentException("Image.RemoveCallbacks: No callback registered for userdata");
            }
            NativeMethods.sp_image_remove_load_callback(this._handle, ImageDelegates.Callback, nativeUserdata);
            ListenerTable.RemoveListener(this._handle, nativeUserdata);
        }
        public string[] Subscribers()
        {
            IntPtr subscribers = NativeMethods.sp_playlist_subscribers(this._handle);
            string[] retval = SpotifyMarshalling.SubscribersToStrings(subscribers);
            var error = NativeMethods.sp_playlist_subscribers_free(subscribers);
            SpotifyMarshalling.CheckError(error);
            return retval;
        }

        public byte[] Data()
        {
            UIntPtr size = UIntPtr.Zero;
            IntPtr ptr = NativeMethods.sp_image_data(this._handle, ref size);
            byte[] data = new byte[(int)size];
            Marshal.Copy(ptr, data, 0, (int)size);
            return data;
        }

        public ImageId ImageId()
        {
            return new ImageId(NativeMethods.sp_image_image_id(this._handle));
        }

        public static Image Create(SpotifySession session, ImageId image_id)
        {
            using (var id = image_id.Lock())
            {
                return new Image(NativeMethods.sp_image_create(session._handle, id.Ptr));
            }
        }
    }

    static class ImageDelegates
    {
        public static readonly image_loaded_cb Callback = image_loaded;

        struct ImageAndListener
        {
            public Image Image;
            public ImageLoaded Listener;
            public object Userdata;
        }
        static ImageAndListener GetListener(IntPtr nativeImage, IntPtr userdata)
        {
            ImageAndListener retVal = new ImageAndListener();
            retVal.Image = new Image(nativeImage);
            if (!Image.ListenerTable.TryGetListenerFromNativeUserdata(userdata, out retVal.Listener, out retVal.Userdata))
            {
                Debug.Fail("Received callback from native code, but no callbacks are registed.");
            }
            return retVal;
        }
        static void image_loaded(IntPtr @image, IntPtr @userdata)
        {
            var context = GetListener(image, userdata);
            context.Listener(context.Image, context.Userdata);
        }
    }
    public delegate void ImageLoaded(Image image, object userdata);
}