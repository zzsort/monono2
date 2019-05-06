using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using monono2.Common.Navigation;

namespace monono2.Common
{
    public class H32Loader
    {
        public List<Vector3> vertices;
        public int width;
        public bool isEmpty = true;
        private HashSet<int> cutoutIndexes = new HashSet<int>();

        public H32Loader(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                Load(fs);
            }
        }

        public H32Loader(Stream stream)
        {
            Load(stream);
        }
        
        private void Load(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                width = (int)Math.Sqrt(br.BaseStream.Length / 3);
                vertices = new List<Vector3>(width * width);

                int x = 0;
                int y = 0;
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int p1 = br.ReadUInt16();
                    byte mat = br.ReadByte();
                    if (mat == 0x3F)
                    {
                        cutoutIndexes.Add((int) (br.BaseStream.Position / 3 - 1));
                    }

                    // Detect terrains which have all zero heights, such as the abyss.
                    // AL allows a single nonzero value to set a constant height, but I don't believe any maps use this.
                    if (isEmpty && p1 != 0)
                        isEmpty = false;

                    float z = p1 / 32f;

                    vertices.Add(new Vector3(y * 2, x *2, z));

                    if (++x == width)
                    {
                        x = 0;
                        y++;
                    }
                }
            }
        }

        public Vector3 VertexLookup(int x, int y)
        {
            return vertices[y * width + x];
        }

        public bool IsCutout(int x, int y)
        {
            return cutoutIndexes.Contains(y * width + x);
        }

        // load terrain vertices for collision testing
        public void LoadIntoGeoSpace(GeoSpace geoSpace)
        {
            if (isEmpty)
                return;

            // Rules:
            // - subdivide X & Y to create smaller AABBs for geospace.
            // - remove steep slopes.

            const int subdivide = 8;

            var meshTriangleVertices = new List<Vector3>();

            for (int sectorY = 0; sectorY < width - 1; sectorY += subdivide)
            {
                for (int sectorX = 0; sectorX < width - 1; sectorX += subdivide)
                {
                    meshTriangleVertices.Clear();

                    // can reduce to 2 triangles only if all vertices are the same height and none are removed.
                    // TODO - use a real poly reduction algorithm instead of fixed size.
                    bool optimizationAllowed = true;

                    int endX = Math.Min(width - 1, sectorX + subdivide);
                    int endY = Math.Min(width - 1, sectorY + subdivide);
                    for (int y = sectorY; y < endY; y++)
                    {
                        for (int x = sectorX; x < endX; x++)
                        {
                            var p1 = VertexLookup(x, y);
                            var p2 = VertexLookup(x, y + 1);
                            var p3 = VertexLookup(x + 1, y);
                            var p4 = VertexLookup(x + 1, y + 1);

                            // TODO - replace with a correct slope test.
                            // TODO - test if any maps have unit size other than 2.
                            if (Math.Abs(p1.Z - p2.Z) >= 2 || Math.Abs(p1.Z - p3.Z) >= 2)
                            {
                                optimizationAllowed = false;
                                continue;
                            }

                            meshTriangleVertices.Add(p1);
                            meshTriangleVertices.Add(p3);
                            meshTriangleVertices.Add(p2);

                            meshTriangleVertices.Add(p2);
                            meshTriangleVertices.Add(p4);
                            meshTriangleVertices.Add(p3);
                        }
                    }

                    if (meshTriangleVertices.Count == 0)
                        continue;

                    if (optimizationAllowed)
                    {
                        // if all points have the same Z, replace with 2 triangles.
                        if (meshTriangleVertices.All(v => v.Z == meshTriangleVertices[0].Z))
                        {
                            // don't optimize if there is a mix of 3F and non-3F mats, as this would break cutouts.
                            // a mix would be rare, so for simplicity, just don't optimize when 3F is found.
                            bool anyMat3F = false;
                            for (int y = sectorY; y < endY; y++)
                            {
                                for (int x = sectorX; x < endX; x++)
                                {
                                    if (IsCutout(x, y)) {
                                        anyMat3F = true;
                                        goto exitMatCheck;
                                    }
                                }
                            }
                            exitMatCheck:


                            if (!anyMat3F)
                            {
                                var p1 = VertexLookup(sectorX, sectorY);
                                var p2 = VertexLookup(sectorX, endY);
                                var p3 = VertexLookup(endX, sectorY);
                                var p4 = VertexLookup(endX, endY);

                                if (p1.Z != meshTriangleVertices[0].Z ||
                                    p2.Z != meshTriangleVertices[0].Z ||
                                    p3.Z != meshTriangleVertices[0].Z ||
                                    p4.Z != meshTriangleVertices[0].Z)
                                    throw new InvalidOperationException("Z mismatch");

                                meshTriangleVertices.Clear();

                                meshTriangleVertices.Add(p1);
                                meshTriangleVertices.Add(p3);
                                meshTriangleVertices.Add(p2);

                                meshTriangleVertices.Add(p2);
                                meshTriangleVertices.Add(p4);
                                meshTriangleVertices.Add(p3);
                            }
                        }
                    }

                    geoSpace.AddCollidableMeshToTree(meshTriangleVertices);
                }
            }
        }
    }
}
