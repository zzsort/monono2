using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace monono2.Common.Navigation
{
    public class NavMeshCompiler
    {
        private int m_blockWidth;
        private int m_blockHeight;
        private List<NavMeshBuilderFloorDesc>[] m_floorData;
        private float m_step;
        private float m_maxZStep;

        public NavMeshCompiler(int blockWidth, int blockHeight, 
            List<NavMeshBuilderFloorDesc>[] floorData,
            float step, float maxZStep)
        {
            m_blockWidth = blockWidth;
            m_blockHeight = blockHeight;
            m_floorData = floorData;
            m_step = step;
            m_maxZStep = maxZStep;
        }

        /*
        height grid:
        each entry:
        0xFF000000 - number of heights (if <= 1, inline. else index to list).
        0x00FFFFFF - index to heights, or 3 byte single height. max distinct = 16 million (4096^2).

        height format (3 bytes):
        0xFF     - 8 directions, from top cw, top TR R BR bot BL left TL.
          0xFFFF - encoded height = (height_value - z1) * 0xFFFF / (z2-z1).

        if 8dir = 0xFF, space is impassable. impassable should always be inline (no index) with floor count 0.

        each node edge is implied by neighbor having a close height that is not blocked in that direction. 
        */
        public CompiledNavMesh Build(HashSet<EdgeVertex> subgraph,
            int startBX, int startBY, int newBlockWidth, int newBlockHeight, 
            float x1, float y1, // world XY of the lower bound of the mesh bounding box.
            int z1, int z2) // min and max z values, range is used for encoding height
        {
            if (startBX < 0 || startBY < 0 || newBlockWidth <= 0 || newBlockHeight <= 0 ||
                startBX + newBlockWidth > m_blockWidth || startBY + newBlockHeight > m_blockHeight)
                throw new ArgumentOutOfRangeException();
            if (z1 < 0 || z2 <= z1)
                throw new ArgumentOutOfRangeException();

            int endBX = startBX + newBlockWidth;
            int endBY = startBY + newBlockHeight;

            uint[] grid = new uint[newBlockWidth * newBlockHeight];
            var multiheights = new List<byte>(newBlockWidth * newBlockHeight); // arbitrary capacity

            var bytes = new List<byte>(); // temp storage per cell

            int iSub = 0; // index to new grid for subgraph

            for (int y = startBY; y < endBY; y++)
            {
                int iAll = y * m_blockWidth + startBX; // index to floor cell

                for (int x = startBX; x < endBX; x++, iAll++, iSub++)
                {
                    bytes.Clear();
                    if (m_floorData[iAll] != null)
                    {
                        foreach (var floor in m_floorData[iAll])
                        {
                            ushort encHeight = NavMeshUtil.EncodeHeight(z1, z2, floor.Z100i / 100.0f);
                            var flags = floor.DirectionFlags;
                            if (flags == 0xFF)
                                continue;

                            // exclude vertices that are not part of the subgraph.
                            if (!subgraph.Contains(new EdgeVertex { BX = (ushort)x, BY = (ushort)y, Z100i = floor.Z100i }))
                                continue;
                            
                            bytes.Add(flags);
                            bytes.Add((byte)(encHeight));
                            bytes.Add((byte)(encHeight >> 8));
                        }
                    }

                    if (bytes.Count == 3)
                    {
                        grid[iSub] = (1 << 24) | // count 1
                            ((uint)bytes[0] << 16) | // flags
                            ((uint)bytes[2] << 8) | // encoded height
                            bytes[1];
                    }
                    else if (bytes.Count > 3)
                    {
                        if (bytes.Count > 255 * 3)
                            throw new InvalidOperationException("too many heights");
                        
                        int index = StoreBytesInMultiFloorSet(multiheights, bytes);
                        grid[iSub] = (uint)((bytes.Count / 3) << 24) | (uint)index;
                    }
                    else
                    {
                        grid[iSub] = 0x00FF0000;
                    }
                }
            }

            if (multiheights.Count > 0xFFFFFF)
                throw new InvalidOperationException("height limit exceeded");

            Debug.WriteLine($"Compiled NavMesh {newBlockWidth}x{newBlockHeight}x{z2-z1} bytes: grid:{grid.Length*4}, Zs:{multiheights.Count}");

            var compiledNavMesh = new CompiledNavMesh(newBlockWidth, newBlockHeight, m_step, m_maxZStep, x1, y1, z1, z2, grid, multiheights.ToArray());
            compiledNavMesh.Validate();
            return compiledNavMesh;
        }

        private int StoreBytesInMultiFloorSet(List<byte> multiheights, List<byte> bytes)
        {
            // TODO - count number of dupes found -- if it is low - remove this code.
            // dedupe will probably not help much now that subgraphs are split.

            goto SKIP_DEDUPE;

            // dedupe repeated byte sequence...
            var end = multiheights.Count - bytes.Count;
            for (int j = 0; j < end; j++)
            {
                if (multiheights[j] == bytes[0])
                {
                    for (int i = 1; i < bytes.Count; i++)
                    {
                        if (multiheights[j + i] != bytes[i])
                        {
                            goto next;
                        }
                    }
                    return j;
                }
                next:;
            }
            SKIP_DEDUPE:
            int result = multiheights.Count;
            multiheights.AddRange(bytes);
            return result;
        }
    }
}
