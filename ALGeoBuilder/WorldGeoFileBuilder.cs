using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using monono2.Common;

namespace monono2.ALGeoBuilder
{
    public class WorldGeoFileBuilder
    {
        public string ClientLevelId { get; private set; }
        byte[] m_landMapH32;
        BrushLstLoader m_brushLst;
        List<string> m_vegetationCgfFilenames;
        List<ObjectsLstItem> m_objectsLst;

        public WorldGeoFileBuilder(string clientLevelId, byte[] landMapH32, BrushLstLoader brushLst,
            List<string> vegetationCgfFilenames, List<ObjectsLstItem> objectsLst)
        {
            ClientLevelId = clientLevelId;
            m_landMapH32 = landMapH32;
            m_brushLst = brushLst;
            m_vegetationCgfFilenames = vegetationCgfFilenames;
            m_objectsLst = objectsLst;
        }
        
        public void CreateWorldGeoFile(string outputPath, bool noH32, HashSet<string> loadedCgfs, HashSet<string> emptyCgfs)
        {
            string geoFile = Path.Combine(outputPath, ClientLevelId + ".geo");
            using (var geoDataStream = new BinaryWriter(File.Open(geoFile, FileMode.Create)))
            {
                bool h32DataCopied = false;
                if (!noH32 && m_landMapH32 != null)
                {
                    // collect indexes to cutout/removed terrain quads.
                    var cutoutIndexHash = new HashSet<int>();

                    // mesh exists
                    geoDataStream.Write((byte)1);
                    // count of terrain data elements
                    var origH32FileSize = m_landMapH32.Length;
                    geoDataStream.Write((int)(origH32FileSize / 3));
                    
                    // Convert terrain
                    try
                    {
                        var convertedh32 = new byte[origH32FileSize / 3 * 2]; // store 2 bytes for every 3
                        bool isEmpty = true; // ignore flat all-zero terrains such as the abyss

                        int src = 0;
                        int dst = 0;
                        while (src < m_landMapH32.Length)
                        {
                            // short: z height * 32
                            byte p1 = m_landMapH32[src++];
                            byte p2 = m_landMapH32[src++];

                            // extra terrain info. range 0-3F.
                            // < 3F index to material in level data.
                            // = 3F is terrain cutout (remove the quad).
                            byte p3 = m_landMapH32[src++];

                            // collect cutout
                            if (p3 == 0x3F) {
                                cutoutIndexHash.Add(src / 3 - 1);
                            }

                            // short z
                            convertedh32[dst++] = p1;
                            convertedh32[dst++] = p2;

                            if (isEmpty && (p1 != 0 || p2 != 0))
                                isEmpty = false;
                        }
                            
                        if (!isEmpty)
                        {
                            foreach (var b in convertedh32)
                                geoDataStream.Write(b);

                            h32DataCopied = true;
                        }
                        else
                        {
                            Console.WriteLine("*   skipping empty terrain");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: Cannot convert terrain.\nException: " + e);
                        return;
                    }

                    // write sorted cutout data after the terrain heights.
                    geoDataStream.Write((int) cutoutIndexHash.Count);
                    if (cutoutIndexHash.Count > 0)
                    {
                        Console.WriteLine($"*   terrain cutouts: {cutoutIndexHash.Count}");
                        foreach (int cutoutIndex in cutoutIndexHash.OrderBy(o => o))
                        {
                            geoDataStream.Write((int)cutoutIndex);
                        }
                    }
                }

                if (!h32DataCopied)
                {
                    // reset
                    geoDataStream.BaseStream.Position = 0;
                    geoDataStream.BaseStream.SetLength(0);

                    geoDataStream.Write((byte)0); // mesh exists
                    geoDataStream.Write((short)0); // stub
                    geoDataStream.Write((int)0); // no cutout data
                }

                // save meshes info
                if (m_brushLst != null) // prison maps have no brush lst
                {
                    foreach (BrushEntry brush in m_brushLst.brushEntries)
                    {
                        string meshFileName = m_brushLst.brushInfoList[brush.meshIdx].filename;

                        // ignore not loaded or empty
                        if (ShouldSkipMesh(meshFileName, loadedCgfs, emptyCgfs))
                            continue;

                        byte[] meshFileNameBytes = Encoding.ASCII.GetBytes(meshFileName);

                        // mesh file name size
                        geoDataStream.Write((short)meshFileNameBytes.Count());
                        // file name
                        geoDataStream.Write(meshFileNameBytes);

                        // position
                        geoDataStream.Write((float)brush.position.X);
                        geoDataStream.Write((float)brush.position.Y);
                        geoDataStream.Write((float)brush.position.Z);

                        // transform
                        geoDataStream.Write((float)(brush.rotationMatrix.M11));
                        geoDataStream.Write((float)(brush.rotationMatrix.M12));
                        geoDataStream.Write((float)(brush.rotationMatrix.M13));
                        geoDataStream.Write((float)(brush.rotationMatrix.M21));
                        geoDataStream.Write((float)(brush.rotationMatrix.M22));
                        geoDataStream.Write((float)(brush.rotationMatrix.M23));
                        geoDataStream.Write((float)(brush.rotationMatrix.M31));
                        geoDataStream.Write((float)(brush.rotationMatrix.M32));
                        geoDataStream.Write((float)(brush.rotationMatrix.M33));

                        // scale - no scale in brush.lst
                        geoDataStream.Write((float)1.0f);

                        // event type 0-4
                        geoDataStream.Write((byte)brush.eventType);
                    }
                }

                if (m_objectsLst != null)
                {
                    // vegetation - continue outputting in the same format as brush entries
                    foreach (ObjectsLstItem obj in m_objectsLst)
                    {
                        string meshFileName = m_vegetationCgfFilenames[obj.ObjectId];

                        // ignore not loaded or empty
                        if (ShouldSkipMesh(meshFileName, loadedCgfs, emptyCgfs))
                            continue;
                        
                        byte[] meshFileNameBytes = Encoding.ASCII.GetBytes(meshFileName);

                        // mesh file name size
                        geoDataStream.Write((short)meshFileNameBytes.Count());
                        // file name
                        geoDataStream.Write(meshFileNameBytes);

                        // position
                        geoDataStream.Write((float)obj.Position.X);
                        geoDataStream.Write((float)obj.Position.Y);
                        geoDataStream.Write((float)obj.Position.Z);

                        // transform
                        var rot = Matrix.CreateRotationZ(MathHelper.ToRadians(obj.Heading));
                        geoDataStream.Write((float)(rot.M11));
                        geoDataStream.Write((float)(rot.M12));
                        geoDataStream.Write((float)(rot.M13));
                        geoDataStream.Write((float)(rot.M21));
                        geoDataStream.Write((float)(rot.M22));
                        geoDataStream.Write((float)(rot.M23));
                        geoDataStream.Write((float)(rot.M31));
                        geoDataStream.Write((float)(rot.M32));
                        geoDataStream.Write((float)(rot.M33));

                        // scale
                        geoDataStream.Write((float)obj.Scale);

                        // event type, always 0 for veg
                        geoDataStream.Write((byte)0);
                    }
                }
            }
        }
        
        private bool ShouldSkipMesh(string meshFileName, HashSet<string> loadedCgfs, HashSet<string> emptyCgfs)
        {
            string normalizedFilename = Util.NormalizeMeshFilename(meshFileName);
            if (!loadedCgfs.Contains(normalizedFilename))
            {
                Console.WriteLine("*  Warning! cgf was not loaded: " + normalizedFilename);
                return true;
            }
            else if (emptyCgfs.Contains(normalizedFilename))
            {
                return true;
            }
            return false;
        }
    }
}
