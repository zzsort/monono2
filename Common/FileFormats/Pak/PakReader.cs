using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Zlib;

namespace monono2.Common.FileFormats.Pak
{
    public sealed class PakReader : IDisposable
    {
        private PakReaderSlim m_pr;
        public Dictionary<string, PakCentralDirFile> Files { get; private set; }

        public PakReader(string filename)
        {
            m_pr = new PakReaderSlim(filename);
            LoadFileListing();
        }

        public string OriginalPakPath => m_pr.OriginalPakPath;

        private void LoadFileListing()
        {
            var result = new Dictionary<string, PakCentralDirFile>();

            foreach (var cd in m_pr.ReadCentralDir())
            {
                result.Add(cd.filename, cd);
            }

            Files = result;
        }

        public byte[] GetFile(string filename)
        {
            return m_pr.ReadFileBytes(Files[PakUtil.NormalizeFilename(filename)]);
        }

        public void Close()
        {
            if (Files != null)
            {
                Files.Clear();
                Files = null;
            }

            if (m_pr != null)
            {
                m_pr.Close();
            }
        }

        public void Dispose()
        {
            Close();
        }
    }

    public sealed class PakReaderSlim : IDisposable
    {
        private FileStream m_fs;
        private BinaryReader m_br;

        private PakCentralDirEnd m_eocd;
        
        public PakReaderSlim(string filename)
        {
            m_fs = File.OpenRead(filename);
            m_br = new BinaryReader(m_fs);

            ReadCentralDirEnd();
        }

        public string OriginalPakPath { get { return m_fs.Name; } }

        public void Close()
        {
            if (m_br != null)
            {
                m_br.Close();
                m_br = null;
            }

            if (m_fs != null)
            {
                m_fs.Close();
                m_fs = null;
            }
        }


        private class EncryptedAionPakReader : Stream
        {
            Stream m_underlying;
            PakCentralDirFile m_dirfile;
            long m_startPosition;
            long m_currentPosition;

            public EncryptedAionPakReader(Stream stream, PakCentralDirFile dirfile)
            {
                m_underlying = stream;
                m_startPosition = stream.Position;
                m_dirfile = dirfile;

                if (!dirfile.isAionFormat)
                    throw new InvalidOperationException("zip stream is not encrypted!");
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;

            public override long Length => m_dirfile.uncompressedSize;

            public override long Position { get => m_currentPosition; set => throw new NotImplementedException(); }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesRead = 0;
                if (m_currentPosition < 32)
                {
                    int tbloff = (int)m_dirfile.compressedSize & 0x3FF;
                    while (m_currentPosition < 32 && bytesRead < count)
                    {
                        int c = m_underlying.ReadByte();
                        if (c == -1)
                            break;

                        // decode byte
                        byte b = (byte)c;
                        b ^= PakConstants.table2[tbloff + m_currentPosition];
                        
                        buffer[offset + bytesRead] = b;

                        m_currentPosition++;
                        bytesRead++;
                    }
                    return bytesRead;
                }
                
                bytesRead = m_underlying.Read(buffer, offset, count);
                m_currentPosition += bytesRead;
                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }

        private void DecodeAionBytes(PakCentralDirFile dirfile, byte[] bytesToModify)
        {
            int tbloff = (int)dirfile.compressedSize & 0x3FF;
            for (int i = 0; i < bytesToModify.Length && i < 32; i++)
                bytesToModify[i] ^= PakConstants.table2[tbloff + i];
        }

        public byte[] ReadFileBytes(PakCentralDirFile dirfile)
        {
            m_fs.Seek(dirfile.localHeaderOffset, SeekOrigin.Begin);
            var fileheader = PakFileEntry.Read(m_br);

            if (!dirfile.filename.Equals(fileheader.filename, StringComparison.OrdinalIgnoreCase) ||
                dirfile.compressedSize != fileheader.compressedSize ||
                dirfile.uncompressedSize != fileheader.uncompressedSize ||
                dirfile.compressionMethod != fileheader.compressionMethod)
            {
                throw new InvalidOperationException("header mismatch");
            }

            byte[] result;
            if (dirfile.compressionMethod == 0)
            {
                result = m_br.ReadBytes((int)dirfile.compressedSize);
                if (dirfile.isAionFormat)
                {
                    DecodeAionBytes(dirfile, result);
                }
            }
            else if (dirfile.compressionMethod == 8)
            {
                Stream zip = (dirfile.isAionFormat ? new EncryptedAionPakReader(m_fs, dirfile) : (Stream)m_fs);
                using (var tmp = new DeflateStream(zip, CompressionMode.Decompress, true))
                {
                    var br = new BinaryReader(tmp);
                    result = br.ReadBytes((int)dirfile.uncompressedSize);
                }
            }
            else
                throw new InvalidOperationException("unsupported compression method");

            return result;
        }
        
        public List<PakCentralDirFile> ReadCentralDir()
        {
            var result = new List<PakCentralDirFile>(m_eocd.thisDiskCentralDirCount);

            m_fs.Seek(m_eocd.centralDirOffset, SeekOrigin.Begin);

            for (int i = 0; i < m_eocd.thisDiskCentralDirCount; i++)
            {
                var dirfile = PakCentralDirFile.Read(m_br);
                result.Add(dirfile);
            }

            return result;
        }

        private void ReadCentralDirEnd()
        {
            m_fs.Seek(-PakCentralDirEnd.HeaderSize, SeekOrigin.End);
            m_eocd = PakCentralDirEnd.Read(m_br);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
