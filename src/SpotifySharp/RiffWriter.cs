using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SpotifySharp
{
    struct RiffId
    {
        readonly uint iId;
        public RiffId(uint aValue)
        {
            iId = aValue;
        }
        public RiffId(string aValue)
        {
            string s = aValue.Length < 4 ? aValue.PadRight(4) : aValue.Substring(0, 4);
            iId = (uint)((s[3] << 24) + (s[2] << 16) + (s[1] << 8) + s[0]);
        }
        public uint Value
        {
            get
            {
                return iId;
            }
        }
        public string String
        {
            get
            {
                return "" + (char)(iId & 0xff) + (char)((iId >> 8) & 0xff) + (char)((iId >> 16) & 0xff) + (char)(iId >> 24);
            }
        }
    }
    class RiffWriter
    {
        List<ListChunk> iParentChunks = new List<ListChunk>();

        public RiffWriter(RiffId aFileType)
        {
            iParentChunks.Add(new ListChunk(new RiffId("RIFF"), aFileType));
        }

        public void OpenListChunk(RiffId aListType)
        {
            iParentChunks.Add(new ListChunk(new RiffId("LIST"), aListType));
        }
        public void CloseListChunk()
        {
            if (iParentChunks.Count <= 1)
            {
                throw new InvalidOperationException("Mismatched CloseListChunk.");
            }
            iParentChunks.RemoveAt(iParentChunks.Count - 1);
        }
        public void AddDataChunk(RiffId aChunkType, byte[] aData)
        {
            iParentChunks[iParentChunks.Count - 1].Add(new DataChunk(aChunkType, aData));
        }
        public void AddDataChunk<T>(RiffId aChunkType, T aData) where T : struct
        {
            int size = Marshal.SizeOf(aData);
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(aData, ptr, false);
            Marshal.Copy(ptr, buffer, 0, size);
            Marshal.FreeHGlobal(ptr);
            iParentChunks[iParentChunks.Count - 1].Add(new DataChunk(aChunkType, buffer));
        }
        public void WriteToFile(BinaryWriter aWriter)
        {
            if (iParentChunks.Count != 1)
            {
                throw new InvalidOperationException("Un-closed chunk at RiffWriter.WriteToFile");
            }
            iParentChunks[0].Write(aWriter);
        }


        abstract class Chunk
        {
            RiffId ChunkType { get; set; }
            int iContentSize;
            protected Chunk(RiffId aChunkType)
            {
                ChunkType = aChunkType;
                iContentSize = 0;
            }
            protected void Reserve(int aSize)
            {
                iContentSize += aSize;
            }
            public int TotalSize
            {
                get
                {
                    return iContentSize + 8;
                }
            }
            public void Write(BinaryWriter aWriter)
            {
                aWriter.Write(ChunkType.Value);
                aWriter.Write(iContentSize);
                WriteContent(aWriter);
            }

            protected abstract void WriteContent(BinaryWriter aWriter);
        }
        class ListChunk : Chunk
        {
            RiffId iListType;
            readonly List<Chunk> iChunks = new List<Chunk>();
            public ListChunk(RiffId aChunkType, RiffId aListType)
                :base(aChunkType)
            {
                iListType = aListType;
                Reserve(4);
            }
            public void Add(Chunk aChunk)
            {
                iChunks.Add(aChunk);
                Reserve(aChunk.TotalSize);
            }
            protected override void WriteContent(BinaryWriter aWriter)
            {
                aWriter.Write(iListType.Value);
                foreach (var subchunk in iChunks)
                {
                    subchunk.Write(aWriter);
                }
            }
        }
        class DataChunk : Chunk
        {
            readonly byte[] iData;
            public DataChunk(RiffId aChunkType, byte[] aData)
                :base(aChunkType)
            {
                iData = aData.ToArray();
                Reserve(aData.Length);
            }
            protected override void WriteContent(BinaryWriter aWriter)
            {
                aWriter.Write(iData);
            }
        }
    }
}