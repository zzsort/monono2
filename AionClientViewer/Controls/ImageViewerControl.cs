using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Forms.Controls;
using monono2.Common;
using Pfim;

namespace monono2.AionClientViewer
{
    public class ImageViewerControl : UpdateWindow
    {
        private Texture2D m_image;
        private VertexBuffer m_buffer;
        private BasicEffect m_basicEffect;
        private Stream m_tempStream;

        private float m_lastScreenWidth;
        private float m_lastScreenHeight;
        private bool m_imageChanged;

        public void SetImage(Stream s)
        {
            if (Editor == null || Editor.graphics == null)
            {
                m_tempStream = new MemoryStream();
                s.CopyTo(m_tempStream);
                m_tempStream.Seek(0, SeekOrigin.Begin);
                return;
            }
            SetImageFromStream(s);
        }

        private bool TryLoadFromKnownMonoImageFormats(Stream s, out Texture2D result)
        {
            try
            {
                result = Texture2D.FromStream(Editor.graphics, s);
            }
            catch (InvalidOperationException e)
            {
                Log.WriteLine("TryLoadFromKnownMonoImageFormats : " + e.Message);
                result = null;
            }
            return (result != null);
        }

        private bool TryLoadDds(Stream s, out Texture2D result)
        {
            try
            {
                s.Seek(0, SeekOrigin.Begin);
                var img = Dds.Create(s, true);

                Texture2D tex = new Texture2D(Editor.graphics, img.Width, img.Height, false, SurfaceFormat.Color);
                if (img.Format == ImageFormat.Rgb24)
                {
                    // convert from 24bit to 32...
                    var convertedData = new byte[img.Width * img.Height * 4];
                    using (var br = new BinaryReader(new MemoryStream(img.Data)))
                    using (var bw = new BinaryWriter(new MemoryStream(convertedData)))
                    {
                        for (int i = 0; i < convertedData.Length; i += 4)
                        {
                            byte r = br.ReadByte();
                            byte g = br.ReadByte();
                            byte b = br.ReadByte();

                            bw.Write(b);
                            bw.Write(g);
                            bw.Write(r);
                            bw.Write((byte)255);
                        }
                    }

                    tex.SetData<byte>(convertedData);
                }
                else if (img.Format == ImageFormat.Rgba32)
                {
                    tex.SetData<byte>(img.Data);
                }
                else
                    throw new InvalidOperationException("unsupported format: " + img.Format);
                
                result = tex;
            }
            catch (Exception e) {

                Log.WriteLine("TryLoadDds : " + e.Message);
                result = null;
            }
            return (result != null);
        }

        private void SetImageFromStream(Stream s)
        {
            Texture2D tex = null;

            // jpg, png, bmp
            TryLoadFromKnownMonoImageFormats(s, out tex);

            // dds
            if (tex == null)
            {
                s.Seek(0, SeekOrigin.Begin);
                if (TryLoadDds(s, out tex))
                {
                    m_image = tex;
                }
            }

            // TODO - pcx, tga

            if (tex == null)
            {
                // image format not supported...
                return;
            }

            m_image = tex;
            m_basicEffect.Texture = m_image;
            m_imageChanged = true;
        }

        protected override void Update(GameTime gameTime)
        {
            if (m_basicEffect == null)
            {
                m_basicEffect = new BasicEffect(Editor.graphics);
                m_basicEffect.View = Matrix.Identity;
                m_basicEffect.TextureEnabled = true;
            }

            if (m_lastScreenWidth != Editor.graphics.Viewport.Width ||
                m_lastScreenHeight != Editor.graphics.Viewport.Height)
            {
                m_basicEffect.Projection = Matrix.CreateOrthographicOffCenter(
                    0, Editor.graphics.Viewport.Width, -Editor.graphics.Viewport.Height, 0, -1, 1);
                m_lastScreenWidth = Editor.graphics.Viewport.Width;
                m_lastScreenHeight = Editor.graphics.Viewport.Height;
            }

            if (m_tempStream != null)
            {
                SetImageFromStream(m_tempStream);
                m_tempStream.Dispose();
                m_tempStream = null;
            }

            if (m_buffer == null)
            {
                m_buffer = new VertexBuffer(Editor.graphics, typeof(VertexPositionTexture), 6, BufferUsage.WriteOnly);
            }

            if (m_imageChanged)
            {
                float w = m_image.Width;
                float h = m_image.Height;
                m_buffer.SetData(new[] {
                    new VertexPositionTexture(new Vector3(0, -h, 0), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(0, 0, 0), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(w, 0, 0), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(0, -h, 0), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(w, 0, 0), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(w, -h, 0), new Vector2(1, 1)),
                });
                m_imageChanged = false;
            }
        }

        protected override void Draw()
        {
            Editor.graphics.Clear(Color.Black);

            if (m_buffer == null || m_image == null)
                return;
            
            Editor.graphics.SetVertexBuffer(m_buffer);
            foreach (EffectPass pass in m_basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, m_buffer.VertexCount / 3);
            }
        }
    }
}
