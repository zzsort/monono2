using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace monono2.Common
{
    public static class Util
    {

        public static BoundingBox GetBoundingBox(IEnumerable<Vector3> vertices)
        {
            var min = Vector3.Zero;
            var max = Vector3.Zero;
            if (vertices.Count() > 0)
            {
                var f = vertices.First();
                min = f;
                max = f;

                foreach (var v in vertices)
                {
                    if (v.X < min.X)
                        min.X = v.X;
                    if (v.Y < min.Y)
                        min.Y = v.Y;
                    if (v.Z < min.Z)
                        min.Z = v.Z;

                    if (v.X > max.X)
                        max.X = v.X;
                    if (v.Y > max.Y)
                        max.Y = v.Y;
                    if (v.Z > max.Z)
                        max.Z = v.Z;
                }
            }
            return new BoundingBox(min, max);
        }

        public static Vector3 GetBoundingBoxCenter(IEnumerable<Vector3> vertices)
        {
            return GetBoundingBoxCenter(GetBoundingBox(vertices));
        }
        public static Vector3 GetBoundingBoxCenter(BoundingBox bbox)
        {
            return Midpoint(bbox.Min, bbox.Max);
        }

        public static void PrintBounds(IEnumerable<Vector3> vertices)
        {
            Debug.WriteLine($"Bounds: ({GetBoundingBox(vertices)})");
        }

        // 1 1 1       1 0 0
        // 0 0 1  -->  1 0 0
        // 0 0 1       1 1 1
        public static void FlipMatrixDiagonal3x3(ref Matrix m)
        {
            float tmp = m.M12;
            m.M12 = m.M21;
            m.M21 = tmp;

            tmp = m.M13;
            m.M13 = m.M31;
            m.M31 = tmp;

            tmp = m.M23;
            m.M23 = m.M32;
            m.M32 = tmp;
        }

        public static Vector3 Midpoint(Vector3 a, Vector3 b)
        {
            return (a + b) * 0.5f;
        }

        public static string NormalizeMeshFilename(string orig)
        {
            return orig.ToLowerInvariant().Replace('\\', '/');
        }

        /*
    public static void DrawACube()
    {

        var vertices = new List<VertexPositionColorTexture>(12);

        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 0, 1), Color.Red, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 1), Color.Red, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 1), Color.Red, new Vector2(1, 1)));

        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 1), Color.White, new Vector2(1, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 0, 1), Color.White, new Vector2(1, 0)));

        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 0, 0), Color.Yellow, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 0), Color.Yellow, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 1), Color.Yellow, new Vector2(1, 1)));

        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 0, 0), Color.Gold, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 1), Color.Gold, new Vector2(1, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 0, 1), Color.Gold, new Vector2(1, 0)));


        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 0, 0), Color.Cyan, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 0), Color.Blue, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 0), Color.Blue, new Vector2(1, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 0, 0), Color.Cyan, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 0), Color.Blue, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 0, 0), Color.Blue, new Vector2(1, 1)));

        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 0, 1), Color.Green, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 1), Color.Blue, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 0), Color.Green, new Vector2(1, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 0, 1), Color.Lime, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 0), Color.Green, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 0, 0), Color.Blue, new Vector2(1, 1)));

        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 0), Color.Black, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 0), Color.Black, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 1), Color.White, new Vector2(1, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 0), Color.Black, new Vector2(0, 0)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(1, 1, 1), Color.White, new Vector2(0, 1)));
        vertices.Add(new VertexPositionColorTexture(new Vector3(0, 1, 1), Color.Black, new Vector2(1, 1)));

        vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), vertices.Count, BufferUsage.WriteOnly);
        vertexBuffer.SetData<VertexPositionColor>(vertices.ToArray());

    }*/


        public static Matrix MatrixFromFloats(float[] f16)
        {
            Matrix result = new Matrix();
            result.M11 = f16[0];
            result.M12 = f16[1];
            result.M13 = f16[2];
            result.M14 = f16[3];
            result.M21 = f16[4];
            result.M22 = f16[5];
            result.M23 = f16[6];
            result.M24 = f16[7];
            result.M31 = f16[8];
            result.M32 = f16[9];
            result.M33 = f16[10];
            result.M34 = f16[11];
            result.M41 = f16[12];
            result.M42 = f16[13];
            result.M43 = f16[14];
            result.M44 = f16[15];
            return result;
        }

        // float[] to matrix:
        //var nodeMatrix = new Matrix();

        /*nodeMatrix.M11 = node.transform[0];
        nodeMatrix.M12 = node.transform[1];
        nodeMatrix.M13 = node.transform[2];
        nodeMatrix.M14 = node.transform[3];
        nodeMatrix.M21 = node.transform[4];
        nodeMatrix.M22 = node.transform[5];
        nodeMatrix.M23 = node.transform[6];
        nodeMatrix.M24 = node.transform[7];
        nodeMatrix.M31 = node.transform[8];
        nodeMatrix.M32 = node.transform[9];
        nodeMatrix.M33 = node.transform[10];
        nodeMatrix.M34 = node.transform[11];
        nodeMatrix.M41 = node.transform[12];
        nodeMatrix.M42 = node.transform[13];
        nodeMatrix.M43 = node.transform[14];
        nodeMatrix.M44 = node.transform[15];*/

        // flip 4x4 diagonal:
        /*nodeMatrix.M11 = node.transform[0];
        nodeMatrix.M12 = node.transform[4];
        nodeMatrix.M13 = node.transform[8];
        nodeMatrix.M14 = node.transform[12];
        nodeMatrix.M21 = node.transform[1];
        nodeMatrix.M22 = node.transform[5];
        nodeMatrix.M23 = node.transform[9];
        nodeMatrix.M24 = node.transform[13];
        nodeMatrix.M31 = node.transform[2];
        nodeMatrix.M32 = node.transform[6];
        nodeMatrix.M33 = node.transform[10];
        nodeMatrix.M34 = node.transform[14];
        nodeMatrix.M41 = node.transform[3];
        nodeMatrix.M42 = node.transform[7];
        nodeMatrix.M43 = node.transform[11];
        nodeMatrix.M44 = node.transform[15];*/

        public static void DrawBoundingBox(BoundingBox bb, List<VertexPositionColor> destLines, Color color)
        {
            var corners = bb.GetCorners();

            destLines.Add(new VertexPositionColor(corners[0], color));
            destLines.Add(new VertexPositionColor(corners[1], color));
            destLines.Add(new VertexPositionColor(corners[1], color));
            destLines.Add(new VertexPositionColor(corners[2], color));
            destLines.Add(new VertexPositionColor(corners[2], color));
            destLines.Add(new VertexPositionColor(corners[3], color));
            destLines.Add(new VertexPositionColor(corners[3], color));
            destLines.Add(new VertexPositionColor(corners[0], color));

            destLines.Add(new VertexPositionColor(corners[4], color));
            destLines.Add(new VertexPositionColor(corners[5], color));
            destLines.Add(new VertexPositionColor(corners[5], color));
            destLines.Add(new VertexPositionColor(corners[6], color));
            destLines.Add(new VertexPositionColor(corners[6], color));
            destLines.Add(new VertexPositionColor(corners[7], color));
            destLines.Add(new VertexPositionColor(corners[7], color));
            destLines.Add(new VertexPositionColor(corners[4], color));
            
            destLines.Add(new VertexPositionColor(corners[0], color));
            destLines.Add(new VertexPositionColor(corners[4], color));
            destLines.Add(new VertexPositionColor(corners[1], color));
            destLines.Add(new VertexPositionColor(corners[5], color));
            destLines.Add(new VertexPositionColor(corners[2], color));
            destLines.Add(new VertexPositionColor(corners[6], color));
            destLines.Add(new VertexPositionColor(corners[3], color));
            destLines.Add(new VertexPositionColor(corners[7], color));
        }

        public static Vector3 ParseVector(string xyzStr)
        {
            var a = xyzStr.Split(new[] { ',' });
            float x = float.Parse(a[0]);
            float y = float.Parse(a[1]);
            float z = float.Parse(a[2]);
            return new Vector3(x, y, z);
        }

        public static void ParseVector(string xyzStr, out float x, out float y, out float z)
        {
            var a = xyzStr.Split(new[] { ',' });
            x = float.Parse(a[0]);
            y = float.Parse(a[1]);
            z = float.Parse(a[2]);
        }
    }
    
    public static class MathUtil
    {
        public static Vector3 CalculateNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            return Vector3.Normalize(Vector3.Cross(a - c, a - b));
        }
    }

    // Ray with a length.
    public class RayX
    {
        Vector3 m_origin;
        Vector3 m_direction;
        public float Limit;

        public RayX(Vector3 origin, Vector3 direction, float limit)
        {
            m_origin = origin;
            m_direction = direction;
            m_direction.Normalize();
            Limit = limit;
        }
        
        public BoundingBox GetBoundingBox()
        {
            var d = Vector3.Normalize(m_direction) * Limit + m_origin;

            float x1 = Math.Min(m_origin.X, d.X);
            float x2 = Math.Max(m_origin.X, d.X);
            float y1 = Math.Min(m_origin.Y, d.Y);
            float y2 = Math.Max(m_origin.Y, d.Y);
            float z1 = Math.Min(m_origin.Z, d.Z);
            float z2 = Math.Max(m_origin.Z, d.Z);

            return new BoundingBox(new Vector3(x1, y1, z1), new Vector3(x2, y2, z2));
        }
        
        // From AL40 / jme3
        public float IntersectsTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            float edge1X = v1.X - v0.X;
            float edge1Y = v1.Y - v0.Y;
            float edge1Z = v1.Z - v0.Z;

            float edge2X = v2.X - v0.X;
            float edge2Y = v2.Y - v0.Y;
            float edge2Z = v2.Z - v0.Z;

            float normX = ((edge1Y * edge2Z) - (edge1Z * edge2Y));
            float normY = ((edge1Z * edge2X) - (edge1X * edge2Z));
            float normZ = ((edge1X * edge2Y) - (edge1Y * edge2X));

            float dirDotNorm = m_direction.X * normX + m_direction.Y * normY + m_direction.Z * normZ;

            float diffX = m_origin.X - v0.X;
            float diffY = m_origin.Y - v0.Y;
            float diffZ = m_origin.Z - v0.Z;

            float sign;
            if (dirDotNorm > float.Epsilon)
            {
                sign = 1;
            }
            else if (dirDotNorm < -float.Epsilon)
            {
                sign = -1f;
                dirDotNorm = -dirDotNorm;
            }
            else
            {
                // ray and triangle/quad are parallel
                return float.PositiveInfinity;
            }

            float diffEdge2X = ((diffY * edge2Z) - (diffZ * edge2Y));
            float diffEdge2Y = ((diffZ * edge2X) - (diffX * edge2Z));
            float diffEdge2Z = ((diffX * edge2Y) - (diffY * edge2X));

            float dirDotDiffxEdge2 = sign * (m_direction.X * diffEdge2X + m_direction.Y * diffEdge2Y + m_direction.Z * diffEdge2Z);

            if (dirDotDiffxEdge2 >= 0.0f)
            {
                diffEdge2X = ((edge1Y * diffZ) - (edge1Z * diffY));
                diffEdge2Y = ((edge1Z * diffX) - (edge1X * diffZ));
                diffEdge2Z = ((edge1X * diffY) - (edge1Y * diffX));

                float dirDotEdge1xDiff = sign * (m_direction.X * diffEdge2X + m_direction.Y * diffEdge2Y + m_direction.Z * diffEdge2Z);

                if (dirDotEdge1xDiff >= 0.0f)
                {
                    if (dirDotDiffxEdge2 + dirDotEdge1xDiff <= dirDotNorm)
                    {
                        float diffDotNorm = -sign * (diffX * normX + diffY * normY + diffZ * normZ);
                        if (diffDotNorm >= 0.0f)
                        {
                            // ray intersects triangle
                            // fill in.
                            float inv = 1f / dirDotNorm;
                            float t = diffDotNorm * inv;
                            return t;
                        }
                    }
                }
            }

            return float.PositiveInfinity;
        }
    }
}
