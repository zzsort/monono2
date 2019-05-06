using System;
using System.IO;
using monono2.Common.BinaryXml;

namespace monono2.Common
{
    // Stream wrapper that automatically decodes binary xml.
    public class AionXmlStreamReader : Stream
    {
        private Stream m_stream;

        public AionXmlStreamReader(Stream stream, bool includeBom)
        {
            try
            {
                bool isBinaryXml = stream.ReadByte() == 128;
                stream.Position = 0;

                if (isBinaryXml)
                {
                    // decode and replace m_stream
                    var xmlStream = BinaryXmlDecoder.Decode(new BinaryReader(stream), includeBom);
                    stream.Close();

                    m_stream = xmlStream;
                }
                else
                {
                    m_stream = stream;
                }
            }
            catch
            {
                stream.Close();
                throw;
            }
        }

        public override bool CanRead => m_stream.CanRead;

        public override bool CanSeek => m_stream.CanSeek;

        public override bool CanWrite => m_stream.CanWrite;

        public override long Length => m_stream.Length;

        public override long Position { get => m_stream.Position; set => m_stream.Position = value; }

        public override void Flush()
        {
            m_stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return m_stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return m_stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            m_stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_stream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (m_stream != null)
            {
                m_stream.Close();
                m_stream = null;
            }
            base.Dispose(disposing);
        }
    }
}
