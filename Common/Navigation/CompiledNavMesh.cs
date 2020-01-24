using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace monono2.Common.Navigation
{

    public class CompiledNavMeshSet
    {
        public List<CompiledNavMesh> Subgraphs;
        public CompiledNavMeshSet(List<CompiledNavMesh> compiledNavMeshes)
        {
            Subgraphs = compiledNavMeshes;
        }

        public void Save(Stream s)
        {
            var bw = new BinaryWriter(s);
            bw.Write(0x30344C41);
            bw.Write(Subgraphs.Count);
            foreach (var sg in Subgraphs)
                sg.Save(s);
        }

        public static CompiledNavMeshSet Load(Stream s)
        {
            var br = new BinaryReader(s);
            if (br.ReadInt32() != 0x30344C41)
                throw new InvalidOperationException("invalid file format - expected CompiledNavMesh header");
            int count = br.ReadInt32();
            var list = new List<CompiledNavMesh>(count);
            for (int i = 0; i < count; i++)
                list.Add(CompiledNavMesh.Load(s));
            return new CompiledNavMeshSet(list);
        }
        
        public CompiledNavMesh FindSubgraphUnderPoint(float x, float y, float z, float maxFall = 20f)
        {
            CompiledNavMesh bestGraph = null;
            float bestDistance = maxFall;
            foreach (var sg in Subgraphs)
            {
                if (!sg.BoundsContainsXY(x, y))
                    continue;
                var node = sg.FindFloorUnderPoint(x, y, z, maxFall);
                if (node.blockIndex != -1)
                {
                    float hitZ = NavMeshUtil.DecodeHeight(sg.Z1, sg.Z2, node.encZ);
                    if (hitZ <= z)
                    {
                        float d = z - hitZ;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            bestGraph = sg;
                        }
                    }
                }
            }
            return bestGraph;
        }

        // if a direct floor is not found, this tries to find a neighboring node to snap to.
        public CompiledNavMesh FindSubgraphUnderPointWithSnap(float x, float y, float z, float maxFall = 20f)
        {
            var result = FindSubgraphUnderPoint(x, y, z, maxFall);
            if (result == null)
            {
                // TODO - this should be half of a step, but step belongs to CompiledNavMesh, not this
                // CompiledNavMeshSet, so it is not available... hardcoding for now...
                float halfStep = 1.0f / 2;

                // TODO - this should only test coords if the BlockXY changes, so the logic
                // needs to be split out more. doing redundant work for now...
                result = FindSubgraphUnderPoint(x + halfStep, y, z, maxFall);
                if (result == null)
                {
                    result = FindSubgraphUnderPoint(x - halfStep, y, z, maxFall);
                    if (result == null)
                    {
                        result = FindSubgraphUnderPoint(x, y + halfStep, z, maxFall);
                        if (result == null)
                        {
                            result = FindSubgraphUnderPoint(x, y - halfStep, z, maxFall);
                        }
                    }
                }
            }
            return result;
        }

        public int GetEstimatedFileSizeInBytes()
        {
            return 8 + // id, count
                Subgraphs.Sum(o => o.GetEstimatedFileSizeInBytes());
        }
    }


    public class CompiledNavMesh
    {
        private ushort m_blockWidth;
        private ushort m_blockHeight;
        private uint[] m_grid;
        private byte[] m_multiheights;
        private float m_step;
        private float m_maxZStep;
        private float m_x1; // world min x
        private float m_y1; // world min y
        private float m_x2; // world max x - computed
        private float m_y2; // world max y - computed
        private int m_z1; // world min z
        private int m_z2; // world max z

        // Note: it is safe to test this class against default(NodeId) for a null test 
        // because xy=0,0 is the min (also index=0), and so directionFlags=0 is invalid here.
        public struct NodeId
        {
            public int blockIndex; // index
            public ushort encZ; // encoded height
            public byte directionFlags;
            public NodeId(int blockIndex, ushort encodedZ, byte directionFlags)
            {
                this.blockIndex = blockIndex;
                this.encZ = encodedZ;
                this.directionFlags = directionFlags;
            }
        }

        public CompiledNavMesh(int blockWidth, int blockHeight, float step, float maxZStep, float x1, float y1, int z1, int z2, uint[] grid, byte[] multiheights)
        {
            m_blockWidth = Convert.ToUInt16(blockWidth);
            m_blockHeight = Convert.ToUInt16(blockHeight);
            m_grid = grid;
            m_multiheights = multiheights;
            m_step = step;
            m_x1 = x1;
            m_y1 = y1;
            m_x2 = x1 + blockWidth * step;
            m_y2 = y1 + blockHeight * step;
            m_z1 = z1;
            m_z2 = z2;
            m_maxZStep = maxZStep;
        }

        public int BlockWidth { get { return m_blockWidth; } }
        public int BlockHeight { get { return m_blockHeight; } }
        public float Step { get { return m_step; } }
        public float X1 { get { return m_x1; } }
        public float Y1 { get { return m_y1; } }
        public int Z1 { get { return m_z1; } }
        public int Z2 { get { return m_z2; } }

        public bool BoundsContainsXY(float x, float y)
        {
            return x >= m_x1 && x < m_x2 && y >= m_y1 && y < m_y2;
        }

        public void Save(Stream s)
        {
            var bw = new BinaryWriter(s);
            bw.Write((uint)0x4D76614E);
            bw.Write((ushort)m_blockWidth);
            bw.Write((ushort)m_blockHeight);
            bw.Write((float)m_step);
            bw.Write((float)m_maxZStep);
            bw.Write((float)m_x1);
            bw.Write((float)m_y1);
            bw.Write((int)m_z1);
            bw.Write((int)m_z2);
            foreach (var g in m_grid)
                bw.Write((uint)g);
            bw.Write((int)m_multiheights.Length);
            bw.Write(m_multiheights);
        }

        public static CompiledNavMesh Load(Stream s)
        {
            var br = new BinaryReader(s);
            if (br.ReadInt32() != 0x4D76614E)
                throw new InvalidOperationException("invalid file format - expected CompiledNavMesh header");
            ushort blockWidth = br.ReadUInt16();
            ushort blockHeight = br.ReadUInt16();
            if (blockWidth == 0 || blockHeight == 0)
                throw new InvalidOperationException("bad block size");
            float step = br.ReadSingle();
            if (step < 0.1f)
                throw new InvalidOperationException("bad step range");
            float maxZStep = br.ReadSingle();
            if (step < 0 || step > 16f) // arbitrary reasonable upper limit to catch bugs... 
                throw new InvalidOperationException("bad z step");
            float x1 = br.ReadSingle();
            float y1 = br.ReadSingle();
            if (x1 < 0 || y1 < 0)
                throw new InvalidOperationException("bad xy min");
            int z1 = br.ReadInt32();
            int z2 = br.ReadInt32();
            int gridCount = blockWidth * blockHeight;
            var grid = new uint[gridCount];
            for (int i = 0; i < gridCount; i++)
                grid[i] = br.ReadUInt32();
            int multiheightsCount = br.ReadInt32();
            var multiheights = br.ReadBytes(multiheightsCount);

            return new CompiledNavMesh(blockWidth, blockHeight, step, maxZStep, x1, y1, z1, z2, grid, multiheights);
        }

        public int GetEstimatedFileSizeInBytes()
        {
            // rough estimation...
            return 9 * 4 + // fields
                m_grid.Length * 4 +
                m_multiheights.Length;
        }

        public void ForeachHeightAtXY(int blockX, int blockY, Action<int, float> action)
        {
            if (blockX < 0 || blockY < 0 || blockX >= m_blockWidth || blockY >= m_blockHeight)
                throw new ArgumentOutOfRangeException();

            int blockIndex = blockY * m_blockWidth + blockX;

            ForeachHeightAtIndex(blockIndex, (entry) => 
            {
                action(entry.directionFlags, NavMeshUtil.DecodeHeight(m_z1, m_z2, entry.encZ));
                return true; // continue looping
            });
        }

        // if func returns true the caller will continue looping, else false stops the loop.
        private void ForeachHeightAtIndex(int blockIndex, Func<Entry, bool> func)
        {
            var gridEntry = m_grid[blockIndex];

            int count = (int)(gridEntry >> 24);
            if (count == 1)
            {
                var unpackedEntry = Entry.UnpackSingleEntry(gridEntry);
                func(unpackedEntry);
            }
            else if (count > 1)
            {
                int index = (int)(gridEntry & 0xFFFFFF);
                for (int i = 0; i < count; i++, index += 3)
                {
                    var unpackedEntry = Entry.UnpackMultiEntry(m_multiheights, index);
                    if (!func(unpackedEntry))
                        break;
                }
            }
        }

        // bx/by must be offset to neighbor. returns height of neighbor, or -Inf if no edge.
        // there should be 0 or 1 edge per direction. neighboring nodes
        // should have a matching edge flag in the inverse direction.
        public float GetEdge(int startBlockX, int startBlockY, float startFloor, int startFlags, int directionFlag)
        {
            if ((startFlags & directionFlag) != 0)
                return float.NegativeInfinity;

            Point ofs = NavMeshUtil.OffsetFromDirectionFlag(directionFlag);
            uint neighbor = m_grid[(startBlockY + ofs.Y) * m_blockWidth + (startBlockX + ofs.X)];

            int count = (int)(neighbor >> 24);
            if (count == 1)
            {
                var entry = Entry.UnpackSingleEntry(neighbor);
                float height = entry.GetRealHeight(m_z1, m_z2);
                if (Math.Abs(startFloor - height) <= m_maxZStep + 0.1f) // TODO added +.1 for epsilon...
                    return height;
            }
            else if (count > 1)
            {
                int index = (int)(neighbor & 0xFFFFFF);
                for (int i = 0; i < count; i++, index += 3)
                {
                    var entry = Entry.UnpackMultiEntry(m_multiheights, index);
                    float height = entry.GetRealHeight(m_z1, m_z2);
                    if (Math.Abs(startFloor - height) <= m_maxZStep + 0.1f) // TODO added +.1 for epsilon...
                        return height;
                }
            }

            throw new InvalidOperationException("neighbor should exist for direction");
        }

        private struct Entry
        {
            public ushort encZ;
            public byte directionFlags;
            public static Entry UnpackSingleEntry(uint value)
            {
                return new Entry {
                    directionFlags = (byte)(value >> 16),
                    encZ = (ushort)value
                };
            }
            public static Entry UnpackMultiEntry(byte[] multiheights, int index)
            {
                return new Entry
                {
                    directionFlags = multiheights[index],
                    encZ = (ushort)(multiheights[index + 1] | (multiheights[index + 2] << 8))
                };
            }
            public float GetRealHeight(int z1, int z2)
            {
                return NavMeshUtil.DecodeHeight(z1, z2, encZ);
            }
        }

        public void Validate()
        {
            // TODO ensure adjacent direction flags match

            // TODO - steep but not vertical slopes will have Z hits, vertical step will fail,
            // however if neighbors hit at the same height, it will allow horizontal travel even
            // though it is on a steep slope...

            //          c2
            //     p1 *-*-* p2
            //       / / / < - vertical connection not made, too steep.
            //   c1 *-*-*  <- horizontally, the edge connection is made.
            //     / / /
            // p3 *-*-* p4
            //
            // IDEA: per cell, check the slope between edges:(p1,p2)-(p3,p4), if too steep eliminate c1.
            // and (p1-p3)-(p2,p4) if too steep eliminate edge c2.
        }
        
        public NodeId FindFloorUnderPoint(float x, float y, float z, float maxFall)
        {
            int resultIndex = -1;
            ushort encZ = 0;
            byte directionFlags = 0xFF;

            int i = BlockIndexFromWorld(x, y);
            if (i >= 0)
            {
                // foreach height, find best from z going down to maxFall. if not found, set i = -1
                ForeachHeightAtIndex(i, (entry) => 
                {
                    float floorZ = entry.GetRealHeight(m_z1, m_z2);
                    if (floorZ > z)
                        return true; // too high, continue loop to try next

                    if (z - floorZ <= maxFall)
                    {
                        encZ = entry.encZ;
                        resultIndex = i;
                        directionFlags = entry.directionFlags;
                    }
                    return false; // stop loop
                });
            }
            return new NodeId(resultIndex, encZ, directionFlags);
        }
        
        public NodeId GetNeighbor(NodeId start, int offX, int offY)
        {
            return GetNeighbor(start, NavMeshUtil.DetermineDirection(0, 0, offX, offY));
        }

        public NodeId GetNeighbor(NodeId start, int direction)
        {
            NodeId result = new NodeId(-1, 0, 0xFF);
            if ((int)(start.directionFlags & direction) == 0)
            {
                var offset = NavMeshUtil.OffsetFromDirectionFlag(direction);
                int neighborIndex = start.blockIndex + (offset.Y * m_blockWidth + offset.X);
                ForeachHeightAtIndex(neighborIndex, (entry) =>
                {
                    float height = entry.GetRealHeight(m_z1, m_z2);
                    // add .5 because the step isn't always exact.
                    if (Math.Abs(NavMeshUtil.DecodeHeight(m_z1, m_z2, start.encZ) - height) > m_maxZStep + 0.5f)
                        return true; // keep looping

                    result = new NodeId(neighborIndex, entry.encZ, entry.directionFlags);
                    return false; // stop loop
                });

                if (result.blockIndex == -1)
                    throw new InvalidOperationException("expected to find neighbor");
            }
            return result;
        }

        // use this function to convert a world coord to a block index,
        // it knows how to offset the step correctly.
        // returns -1 on failure.
        private int BlockIndexFromWorld(float x, float y)
        {
            // offset the input by half a step to center over block.
            float halfStep = m_step / 2;

            int bx = (int)((x + halfStep - m_x1 ) / m_step );
            if (bx < 0 || bx >= m_blockWidth)
                return -1;
            int by = (int)((y + halfStep - m_y1 ) / m_step );
            if (by < 0 || by >= m_blockHeight)
                return -1;
            return by * m_blockWidth + bx;
        }

        public Vector3 WorldFromBlockIndex(int i, float passthroughZ)
        {
            Point b = BlockXYFromIndex(i);
            float x = m_x1 + b.X * m_step;
            float y = m_y1 + b.Y * m_step;
            return new Vector3(x, y, passthroughZ);
        }

        public Point BlockXYFromIndex(int i)
        {
            return new Point(i % m_blockWidth, i / m_blockWidth);
        }

        public Vector3 WorldPointFromNode(NodeId node)
        {
            return WorldFromBlockIndex(node.blockIndex, NavMeshUtil.DecodeHeight(m_z1, m_z2, node.encZ));
        }

        int NumberOfSetBits(int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }
        
        // https://www.redblobgames.com/pathfinding/a-star/introduction.html
        public Dictionary<NodeId, NodeId> AStar(NodeId start, NodeId goal)
        {
            var frontier = new PriorityQueue<NodeId>();
            frontier.Push(start, 0);
            var came_from = new Dictionary<NodeId, NodeId>();
            var cost_so_far = new Dictionary<NodeId, float>();
            came_from[start] = default(NodeId);
            cost_so_far[start] = 0;

            float heuristic(NodeId a, NodeId b)
            {
                Point p0 = BlockXYFromIndex(a.blockIndex);
                Point p1 = BlockXYFromIndex(b.blockIndex);
                
                float edist = Vector2.Distance(new Vector2(p0.X, p0.Y), new Vector2(p1.X, p1.Y));
                //float manhattanDist = Math.Abs(p0.X - p1.X) + Math.Abs(p0.Y - p1.Y);

                // Number of bits - favor nodes with more connections.
                float dirWeight = NumberOfSetBits(b.directionFlags);

                return edist + dirWeight;
            }

            void test(NodeId current, int direction)
            {
                NodeId next = GetNeighbor(current, direction);
                if (next.blockIndex == -1)
                    return;
                float dirCost = (direction & 0xAA) != 0 ? 1.4142f : 1f;
                dirCost += NumberOfSetBits(next.directionFlags) / 2f;

                float new_cost = cost_so_far[current] + dirCost; //heuristic(current, next);
                if (!cost_so_far.ContainsKey(next) || new_cost < cost_so_far[next])
                {
                    cost_so_far[next] = new_cost;
                    // *10 seems to give better routes, reduce iterations...
                    int priority = (int)( (new_cost + heuristic(goal, next)) * 10);
                    frontier.Push(next, priority);
                    came_from[next] = current;
                }
            }

            int iterationCount = 0;

            while (!frontier.Empty())
            {
                NodeId current = frontier.Pop();
                if (current.Equals(goal))
                    break;

                test(current, NavMeshUtil.DIRECTION_TOP);
                test(current, NavMeshUtil.DIRECTION_TL);
                test(current, NavMeshUtil.DIRECTION_RIGHT);
                test(current, NavMeshUtil.DIRECTION_BR);
                test(current, NavMeshUtil.DIRECTION_BOTTOM);
                test(current, NavMeshUtil.DIRECTION_BL);
                test(current, NavMeshUtil.DIRECTION_LEFT);
                test(current, NavMeshUtil.DIRECTION_TR);

                iterationCount++;
            }

            Debug.WriteLine("AStar iteration count: " + iterationCount);

            return came_from;
        }

        // AL40 impl ported from java.
        public List<Vector3> findPath(NodeId start, NodeId goal, Vector3 exactEnd)
        {
            var graph = AStar(start, goal);
            if (graph == null)
            {
                return null;
            }

            var path = new List<Vector3>();

            // gather the steps. these are reversed...
            NodeId currentNode = goal;
            while (true)
            {
                NodeId next = graph[currentNode]; // walks backwards from goal, so really 'prev'
                if (next.blockIndex < 0)
                {
                    Debug.WriteLine("NavMesh.FindPath - node not found??");
                    break;
                }

                path.Add(WorldPointFromNode(currentNode));

                // done if on start. the start point must be added to the list
                // to properly reduce later.
                if (next.Equals(start))
                    break;

                currentNode = next;
            }

            // reverse the list so points go from start to goal.

            // TODO - could reducePath here, and just traverse the path in
            // reverse and build the smoothed path in the correct order.

            var result = new List<Vector3>(path.Count);
            for (int i = path.Count() - 1; i >= 0; i--)
            {
                result.Add(path[i]);
            }

            result = reducePath(result);

            // replace last point with exact end point
            if (result.Count > 0)
            {
                result.RemoveAt(result.Count - 1);
            }
            result.Add(exactEnd);

            return result;
        }
        
        // TODO - need to avoid creating lines that cross blocked node edges.
        private List<Vector3> reducePath(List<Vector3> original)
        {

            if (original.Count < 2)
            {
                if (original.Count > 0)
                    original.RemoveAt(0); // remove redundant starting point
                return original;
            }

            var result = new List<Vector3>();

            int lastDirection = 0;
            int primaryDir = 0;
            int secondaryDir = 0;
            int majorDir = 0; // this is the direction that is allowed to repeat.
            float previousDeltaZ = Math.Abs(original[0].Z - original[1].Z);

            for (int i = 2; ; i++)
            {
                if (i >= original.Count)
                {
                    result.Add(original.Last());
                    break;
                }

                Vector3 previousPoint = original[i - 1];
                Vector3 currentPoint = original[i];
                int direction = NavMeshUtil.determineDirectionFromWorld(original[i - 1], original[i]);

                float currentDeltaZ = Math.Abs(original[i].Z - original[i - 1].Z);

                bool emit = true;

                // if no major Z difference, check for continuous line.
                if (Math.Abs(currentDeltaZ - previousDeltaZ) < 0.2f ) // TODO - tune this number - check height/smoothness in client.
                {
                    emit = false;
                    if (lastDirection == 0)
                    {
                        // first time through
                        primaryDir = direction;
                    }
                    else if (secondaryDir == 0 && NavMeshUtil.isDirectionAdjacent(lastDirection, direction))
                    {
                        // init secondary on first adjacent step
                        secondaryDir = direction;
                    }
                    else if (majorDir == 0 && lastDirection == direction)
                    {
                        // init major dir on first double sequence
                        majorDir = direction;
                    }
                    else if ((direction == majorDir) ||
                        (direction == primaryDir && lastDirection == secondaryDir) ||
                        (direction == secondaryDir && lastDirection == primaryDir))
                    {
                        // allow step
                    }
                    else
                    {
                        emit = true;
                    }
                }

                lastDirection = direction;
                previousDeltaZ = currentDeltaZ;

                if (emit)
                {
                    // emit the previous point.
                    result.Add(previousPoint);
                
                    primaryDir = 0;
                    secondaryDir = 0;
                    majorDir = 0;
                }
            }

            return result;
        }

        // returns a direction if indexes are neighbors, else -1
        // Does not check if the direction is open.
        public int getDirectionToNeighborByIndex(int startIndex, int endIndex)
        {
            if (endIndex < startIndex)
            {
                Point p = BlockXYFromIndex(startIndex);
                if (p.X > 0 && endIndex == startIndex - 1)
                    return NavMeshUtil.DIRECTION_LEFT;
                if (p.Y > 0)
                {
                    int down = startIndex - m_blockWidth;
                    if (endIndex == down)
                        return NavMeshUtil.DIRECTION_BOTTOM;
                    if (p.X > 0 && endIndex == (down - 1))
                        return NavMeshUtil.DIRECTION_BL;
                    if (p.X < (m_blockWidth - 1) && endIndex == (down + 1))
                        return NavMeshUtil.DIRECTION_BR;
                }
            }
            else if (endIndex > startIndex)
            {
                Point p = BlockXYFromIndex(startIndex);
                if (p.X < (m_blockWidth - 1) && endIndex == (startIndex + 1))
                    return NavMeshUtil.DIRECTION_RIGHT;
                if (p.Y < (m_blockHeight - 1))
                {
                    int top = startIndex + m_blockWidth;
                    if (endIndex == top)
                        return NavMeshUtil.DIRECTION_TOP;
                    if (p.X > 0 && endIndex == (top - 1))
                        return NavMeshUtil.DIRECTION_TL;
                    if (p.X < (m_blockWidth - 1) && endIndex == (top + 1))
                        return NavMeshUtil.DIRECTION_TR;
                }
            }
            return -1;
        }
    }
}
