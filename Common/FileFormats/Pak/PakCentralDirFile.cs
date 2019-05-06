using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace monono2.Common.FileFormats.Pak
{
    public class PakCentralDirFile
    {
        public bool isAionFormat; // true for aion header, false for zip format

        public ushort signature1;
        public ushort signature2;
        public ushort createVersion;
        public ushort extractVersion;
        public ushort flags;
        public ushort compressionMethod;
        public ushort time;
        public ushort date;
        public uint crc;
        public uint compressedSize;
        public uint uncompressedSize;
        public ushort filenameLength;
        public ushort extraFieldLength;
        public ushort fileCommentLength;
        public ushort diskNumStart;
        public ushort internalFileAttr;
        public uint externalFileAttr;
        public uint localHeaderOffset;

        public string filename;

        //public byte[] extraField;
        //public byte[] comment;

        public static PakCentralDirFile Read(BinaryReader br)
        {
            var result = new PakCentralDirFile();
            result.signature1 = br.ReadUInt16();
            result.signature2 = br.ReadUInt16();
            result.createVersion = br.ReadUInt16();
            result.extractVersion = br.ReadUInt16();
            result.flags = br.ReadUInt16();
            result.compressionMethod = br.ReadUInt16();
            result.time = br.ReadUInt16();
            result.date = br.ReadUInt16();
            result.crc = br.ReadUInt32();
            result.compressedSize = br.ReadUInt32();
            result.uncompressedSize = br.ReadUInt32();
            result.filenameLength = br.ReadUInt16();
            result.extraFieldLength = br.ReadUInt16();
            result.fileCommentLength = br.ReadUInt16();
            result.diskNumStart = br.ReadUInt16();
            result.internalFileAttr = br.ReadUInt16();
            result.externalFileAttr = br.ReadUInt32();
            result.localHeaderOffset = br.ReadUInt32();

            result.filename = PakUtil.ReadFilename(br, result.filenameLength);

            if (result.signature1 == PakConstants.PAK_SIGNATURE1 &&
                result.signature2 == PakConstants.PAK_SIGNATURE2_DIR)
            {
                result.isAionFormat = true;
            }
            else
            {
                if (result.signature1 != PakConstants.ZIP_SIGNATURE1 ||
                    result.signature2 != PakConstants.ZIP_SIGNATURE2_DIR)
                    throw new InvalidOperationException("bad central dir signature");

                // zipformat = true
            }

            if (result.extraFieldLength != 0)
            {
                var b = br.ReadBytes(result.extraFieldLength);
                //    throw new InvalidOperationException("extra field not supported");
            }

            if (result.fileCommentLength != 0)
                throw new InvalidOperationException("file comment not supported");

            if (result.diskNumStart != 0)
                throw new InvalidOperationException("disk num not supported");

            return result;
        }
    }
}
