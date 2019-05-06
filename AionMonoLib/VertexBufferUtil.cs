using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace monono2.AionMonoLib
{
    // http://www.riemers.net/eng/Tutorials/XNA/Csharp/Series1/Terrain_lighting.php
    public struct VertexPositionColorNormal : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector3 Normal;

        public readonly static VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(sizeof(float) * 3, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(sizeof(float) * 3 + 4, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0));

        public VertexPositionColorNormal(Vector3 pos, Color color, Vector3 normal)
        {
            Position = pos;
            Color = color;
            Normal = normal;
        }

        VertexDeclaration IVertexType.VertexDeclaration
        {
            get { return VertexDeclaration; }
        }
    }

    public static class VertexBufferUtil
    {
        // Splits vertices into multiple buffers to stay within the size limit.
        public static List<VertexBuffer> CreateLargeVertexBuffer<T>(GraphicsDevice GraphicsDevice, IEnumerable<T> v) where T : struct
        {
            // VertexBuffer limit is 128MB:
            // https://docs.microsoft.com/en-us/windows/desktop/direct3d11/overviews-direct3d-11-resources-limits
            // Going over the limit causes the creation of the next VertexBuffer to fail with:
            // HRESULT: [0x887A0005], Module: [SharpDX.DXGI], ApiCode: [DXGI_ERROR_DEVICE_REMOVED/DeviceRemoved], Message: Unknown

            if (v == null || v.Count() == 0)
                return null;
            int maxBufferElements = (128 * 1024 * 1024) / System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
            int maxTriangleVertices = maxBufferElements / 3 * 3;

            var result = new List<VertexBuffer>();

            int vCount = v.Count();
            for (int i = 0; i < vCount; i += maxTriangleVertices)
            {
                int curSize = Math.Min(vCount - i, maxTriangleVertices);
                
                var array = v.Skip(i).Take(curSize).ToArray();

                var vb = new VertexBuffer(GraphicsDevice, typeof(T), curSize, BufferUsage.WriteOnly);
                vb.SetData(array);

                result.Add(vb);
            }
            return result;
        }
    }
}
