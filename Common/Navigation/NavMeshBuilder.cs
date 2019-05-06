using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace monono2.Common.Navigation
{
    public struct NavMeshBuilderFloorDesc
    {
        public NavMeshBuilderFloorDesc(int z100i, byte directionFlags = 0xFF)
        {
            Z100i = z100i;
            DirectionFlags = directionFlags;
        }
        public int Z100i;
        public byte DirectionFlags;
    }

    public struct EdgeVertex
    {
        public ushort BX; // block coordinate
        public ushort BY; // block coordinate
        public int Z100i; // world coordinate * 100

        public override string ToString()
        {
            return $"EdgeVertex: BX:{BX} BY:{BY} WZ:{Z100i / 100.0f}";
        }
    }

    // An axis-aligned 3d grid of floor points. 
    public class NavMeshBuilder
    {
        // min
        private float m_x1;
        private float m_y1;
        private int m_z1;
        // max
        private float m_x2;
        private float m_y2;
        private int m_z2;

        private float m_step;
        private float m_maxZStep;
        private float m_requiredSpaceAboveFloor = 2;

        private int m_blockWidth;
        private int m_blockHeight;
        private List<NavMeshBuilderFloorDesc>[] m_floorData;

        public int BlockWidth { get { return m_blockWidth; } }
        public int BlockHeight { get { return m_blockHeight; } }
        public float Step { get { return m_step; } }

        public static int OPTION_PARALLEL_THREADS = -1;
        public static bool OPTION_REMOVE_SMALL_GRAPHS = true;

        public NavMeshBuilder(float x1, float y1, int z1, float x2, float y2, int z2, float step)
        {
            if (x1 >= x2 || y1 >= y2 || z1 >= z2)
                throw new ArgumentException("xyz1 must be less than xyz2");

            if (x1 < 0 || y1 < 0 || z1 < 0)
                throw new ArgumentOutOfRangeException("world bounds must be positive");

            m_x1 = x1;
            m_y1 = y1;
            m_z1 = z1;
            m_x2 = x2;
            m_y2 = y2;
            m_z2 = z2;
            m_step = step;
            
            // bug... if the resolution is too high, really steep slopes will be allowed...
            // if the resolution is low, the zstep needs to be higher to account for it.
            // (eg, step=4 should be allowed to zstep about 4 units. but step=.1 zstep=.1 is too
            // granular and will probably fail many collision tests that would otherwise pass)
            // changing this value may cause errors...
            m_maxZStep = 0.8f; //Math.Max(1, 0.7f * m_step);

            m_blockWidth = (int)Math.Ceiling((m_x2 - m_x1) / step);
            m_blockHeight = (int)Math.Ceiling((m_y2 - m_y1) / step);
            m_floorData = new List<NavMeshBuilderFloorDesc>[m_blockWidth * m_blockHeight];
        }

        // scans the geospace and collects points where it finds a floor.
        public CompiledNavMeshSet ScanFloor(GeoSpace geoSpace)
        {
            var timer = DateTime.Now;

            // collect the heights at each cell.
            Parallel.For(0, m_blockHeight, new ParallelOptions { MaxDegreeOfParallelism = OPTION_PARALLEL_THREADS }, (blockY) =>
            {
                float y = m_y1 + blockY * m_step;
                int blockX = 0;
                for (float x = m_x1; x < m_x2; x += m_step, blockX++)
                {
                    float curTop = m_z2;
                    while (curTop > m_z1)
                    {
                        var v0 = new Vector3(x, y, curTop);
                        var ray = new RayX(v0, new Vector3(0, 0, -1), (curTop - m_z1));

                        float best = (curTop - m_z1);

                        // descend from top to find vertical hits.
                        geoSpace.DoActionOnIntersectingMeshes(ray.GetBoundingBox(),
                            (Vector3[] points) =>
                            {
                                for (int i = 0; i < points.Length; i += 3)
                                {
                                    float d = ray.IntersectsTriangle(points[i], points[i + 1], points[i + 2]);
                                    if (d < best)
                                    {
                                        best = d;
                                    }
                                }
                                return true; // process all
                            });

                        // no more hits?
                        if (best >= (curTop - m_z1))
                            break;

                        float z = curTop - best;
                        
                        // round-trip the height value
                        //z = NavMeshUtil.DecodeHeight(m_z1, m_z2, NavMeshUtil.EncodeHeight(m_z1, m_z2, z));

                        // add the floor if there is enough room above it.
                        if (!geoSpace.HasCollision(
                            new RayX(new Vector3(x, y, z + 0.01f), // raise z to avoid colliding with start point
                                     new Vector3(0, 0, 1), // look up
                                     m_requiredSpaceAboveFloor)))
                        {
                            AddFloorPoint(x, y, z);
                        }

                        curTop -= best + 1; // +1 to move down to skip some solid space
                    }
                }
            });
            
            Debug.WriteLine("ScanFloor time: " + (DateTime.Now - timer));

            // for each floor in each cell, determine neighbor connections.
            timer = DateTime.Now;
            ComputeDirectionFlags(geoSpace);
            Debug.WriteLine("ComputeDirectionFlags time: " + (DateTime.Now - timer));
            
            // make a temporary map of each vertex to its neighboring vertices.
            // this is used for subgraph calculation.
            timer = DateTime.Now;
            Dictionary<EdgeVertex, List<EdgeVertex>> edges = GatherEdgeVertices();
            Debug.WriteLine("GATHER EDGES time: " + (DateTime.Now - timer));

            /* // PRINT ALL EDGES
            foreach (var kvp in edges)
            {
                Debug.WriteLine(kvp.Key);
                foreach (var v in kvp.Value)
                {
                    Debug.WriteLine("  " + v);
                }
            }*/

            // TODO - why the need to remove steep edges? they should have been excluded when
            // computing directions already...
            // // remove steep edges... modifies edges (and floordata), do this before
            // // computing subgraphs.
            // //timer = DateTime.Now;
            // //RemoveSteepEdges(edges);
            // //Debug.WriteLine("RemoveSteepEdges time: " + (DateTime.Now - timer));

            // separate the subgraphs
            timer = DateTime.Now;
            List<HashSet<EdgeVertex>> subgraphs = ComputeSubgraphs(edges);
            Debug.WriteLine("ComputeSubgraphs time: " + (DateTime.Now - timer));

            // FILTERING

            // filter subgraphs by size
            if (OPTION_REMOVE_SMALL_GRAPHS)
            {
                timer = DateTime.Now;
                subgraphs = subgraphs.Where(vertices => vertices.Count >= 100).ToList();
                Debug.WriteLine("RemoveSmallSubgraphs time: " + (DateTime.Now - timer));
            }
            else
                Debug.WriteLine("NOTE: not removing small graphs");

            // POST FILTERING

            // by now, the gathered subgraph edges may not match the original direction flags,
            // due to slope checks, height checks, etc...
            // so, update the direction flags so they reflect the current gathered subgraphs.
            // it is possible this step could hide bugs, but overall is necessary...
            timer = DateTime.Now;
            FixDirectionFlagsToMatchSubgraphs(edges, subgraphs);
            Debug.WriteLine("FIX DIRECTION FLAGS time: " + (DateTime.Now - timer));

            // VALIDATION

            ValidateAllSubgraphVerticesExistInFloorData(subgraphs);

            ValidateEdgesAtBoundsAreBlocked(subgraphs);

            timer = DateTime.Now;
            var compiledMeshes = new List<CompiledNavMesh>();
            foreach (var sg in subgraphs)
                compiledMeshes.Add(Build(sg));
            var compiledNavMeshSet = new CompiledNavMeshSet(compiledMeshes);
            Debug.WriteLine("Compile time: " + (DateTime.Now - timer));
            return compiledNavMeshSet;
        }

        private void ValidateAllSubgraphVerticesExistInFloorData(List<HashSet<EdgeVertex>> subgraphs)
        {
            // all subgraph vertices should exist in floordata
            foreach (var sg in subgraphs)
            {
                foreach (var v in sg)
                {
                    foreach (var f in m_floorData[v.BY * m_blockWidth + v.BX])
                    {
                        if (f.Z100i == v.Z100i)
                            goto next;
                    }
                    throw new InvalidOperationException("vertex not found!");
                    next:;
                }
            }
        }

        // TODO - update to simply test each edge has matching directions - should accomplish the same thing and cover more cases.
        private void ValidateEdgesAtBoundsAreBlocked(List<HashSet<EdgeVertex>> subgraphs)
        {
            foreach (var sg in subgraphs)
            {
                // gather min/max vertices - these are the outer axial edges of the subgraph.
                int minBX = ushort.MaxValue;
                int maxBX = ushort.MinValue;
                int minBY = ushort.MaxValue;
                int maxBY = ushort.MinValue;

                foreach (var v in sg)
                {
                    minBX = Math.Min(minBX, v.BX);
                    maxBX = Math.Max(maxBX, v.BX);
                    minBY = Math.Min(minBY, v.BY);
                    maxBY = Math.Max(maxBY, v.BY);
                }

                foreach (var v in sg)
                {
                    List<NavMeshBuilderFloorDesc> data = m_floorData[v.BY * m_blockWidth + v.BX];
                    if (v.BX == minBX)
                        foreach (var d in data)
                            if (d.Z100i == v.Z100i)
                                if ((~d.DirectionFlags & (NavMeshUtil.DIRECTION_LEFT| NavMeshUtil.DIRECTION_TL | NavMeshUtil.DIRECTION_BL)) != 0)
                                    throw new InvalidOperationException("left should be blocked");
                    if (v.BX == maxBX)
                        foreach (var d in data)
                            if (d.Z100i == v.Z100i)
                                if ((~d.DirectionFlags & (NavMeshUtil.DIRECTION_RIGHT | NavMeshUtil.DIRECTION_TR | NavMeshUtil.DIRECTION_BR)) != 0)
                                    throw new InvalidOperationException("right should be blocked");
                    if (v.BY == minBY)
                        foreach (var d in data)
                            if (d.Z100i == v.Z100i)
                                if ((~d.DirectionFlags & (NavMeshUtil.DIRECTION_BOTTOM | NavMeshUtil.DIRECTION_BL | NavMeshUtil.DIRECTION_BR)) != 0)
                                    throw new InvalidOperationException("bottom should be blocked");
                    if (v.BY == maxBY)
                        foreach (var d in data)
                            if (d.Z100i == v.Z100i)
                                if ((~d.DirectionFlags & (NavMeshUtil.DIRECTION_TOP | NavMeshUtil.DIRECTION_TL | NavMeshUtil.DIRECTION_TR)) != 0)
                                    throw new InvalidOperationException("top should be blocked");
                }
            }
        }

        // WARNING! on failure, m_floorData will be left in a bad state.
        private void FixDirectionFlagsToMatchSubgraphs(Dictionary<EdgeVertex, List<EdgeVertex>> edges,
            List<HashSet<EdgeVertex>> subgraphs)
        {
            // mark all directions as blocked, then for each subgraph
            // visit edges/vertices and unblock directions.
            
            // clear all to 'blocked'
            foreach (var fl in m_floorData)
                if (fl != null)
                    for (int i = 0; i < fl.Count; i++)
                        fl[i] = new NavMeshBuilderFloorDesc(fl[i].Z100i, 0xFF);


            foreach (var sg in subgraphs)
            {
                foreach (var v in sg)
                {
                    var neighborEdges = edges[v];

                    foreach (var n in neighborEdges)
                    {
                        var dirToNeighbor = NavMeshUtil.DetermineDirection(v.BX, v.BY, n.BX, n.BY);
                        var dirFromNeighbor = NavMeshUtil.GetInverseDirection(dirToNeighbor);

                        // update 'to' neighbor
                        // need to find which floor data entry is mine
                        var meContainedInList = m_floorData[v.BY* m_blockWidth +v.BX];
                        // next, find me
                        bool foundMe = false;
                        for (int j = 0; j < meContainedInList.Count; j++)
                        {
                            if (meContainedInList[j].Z100i == v.Z100i)
                            {
                                // unblock me to neighbor
                                meContainedInList[j] = new NavMeshBuilderFloorDesc(v.Z100i,
                                    (byte)(meContainedInList[j].DirectionFlags & ~dirToNeighbor));
                                foundMe = true;
                                break;
                            }
                        }
                        if (!foundMe)
                            throw new InvalidOperationException("vertex should exist in floor data!");
                        
                        // update 'from' neighbor
                        var neighborContainedInList = m_floorData[n.BY * m_blockWidth + n.BX];
                        bool foundNeighbor = false;
                        for (int j = 0; j < neighborContainedInList.Count; j++)
                        {
                            if (neighborContainedInList[j].Z100i == n.Z100i)
                            {
                                // unblock neighbor to me
                                neighborContainedInList[j] = new NavMeshBuilderFloorDesc(n.Z100i,
                                    (byte)(neighborContainedInList[j].DirectionFlags & ~dirFromNeighbor));
                                foundNeighbor = true;
                                break;
                            }
                        }
                        if (!foundNeighbor)
                            throw new InvalidOperationException("neighbor vertex should exist in floor data!");
                        
                    }
                    
                }
            }
        }


        private CompiledNavMesh Build(HashSet<EdgeVertex> subgraph)
        {
            // Compute bounds of the subgraph
            int minBX = int.MaxValue;
            int maxBX = int.MinValue;
            int minBY = int.MaxValue;
            int maxBY = int.MinValue;
            int minZ100i = int.MaxValue;
            int maxZ100i = int.MinValue;
            foreach (var v in subgraph)
            {
                if (v.BX < minBX) minBX = v.BX;
                if (v.BX > maxBX) maxBX = v.BX;
                if (v.BY < minBY) minBY = v.BY;
                if (v.BY > maxBY) maxBY = v.BY;
                if (v.Z100i < minZ100i) minZ100i = v.Z100i;
                if (v.Z100i > maxZ100i) maxZ100i = v.Z100i;
            }
            
            if (minBX > maxBX || minBY > maxBY || minZ100i > maxZ100i)
                throw new InvalidOperationException();

            float x1 = m_x1 + minBX * m_step;
            float y1 = m_y1 + minBY * m_step;

            int z1 = (int)(minZ100i / 100.0f);
            int z2 = (int)(maxZ100i / 100.0f) + 1;

            // Compile
            var compiler = new NavMeshCompiler(m_blockWidth, m_blockHeight, m_floorData, m_step, m_maxZStep);
            var newBlockWidth = maxBX - minBX + 1; // inclusive range
            var newBlockHeight = maxBY - minBY + 1; // inclusive range
            return compiler.Build(subgraph, minBX, minBY, newBlockWidth, newBlockHeight, x1, y1, z1, z2);
        }

        //private void RemoveSteepEdges(Dictionary<EdgeVertex, List<EdgeVertex>> edges)
        //{
        //    // foreach KEY, 
        //    //  if slope is too steep to vertex in VALUE,
        //    //   remove vertex from VALUE
        //    //   find KEY=vertex, remove KEY from vertex's VALUE.
        //    // remove all KEYs with VALUE count = 0
        //    // 
        //}
        
        private List<HashSet<EdgeVertex>> ComputeSubgraphs(Dictionary<EdgeVertex, List<EdgeVertex>> edges)
        {
            var subgraphs = new List<HashSet<EdgeVertex>>();
            var totalVisited = new HashSet<EdgeVertex>();

            foreach (var startVertex in edges.Keys)
            {
                if (totalVisited.Contains(startVertex))
                    continue;

                // start gathering a new subgraph.
                var pleaseVisit = new Queue<EdgeVertex>();
                pleaseVisit.Enqueue(startVertex);

                // walk all connected edges and add to this subgraph.
                var currentSubgraph = new HashSet<EdgeVertex>();
                while (pleaseVisit.Count > 0)
                {
                    EdgeVertex cur = pleaseVisit.Dequeue();
                    if (currentSubgraph.Contains(cur))
                        continue;
                    currentSubgraph.Add(cur);
                    totalVisited.Add(cur);
                    foreach (var v in edges[cur])
                        pleaseVisit.Enqueue(v);
                }

                subgraphs.Add(currentSubgraph);
            }
            return subgraphs;
        }


        private Dictionary<EdgeVertex, List<EdgeVertex>> GatherEdgeVertices()
        {
            var edges = new Dictionary<EdgeVertex, List<EdgeVertex>>();
            for (int blockY = 0; blockY < m_blockHeight; blockY++)
            {
                for (int blockX = 0; blockX < m_blockWidth; blockX++)
                {
                    var center = GetFloorDescListNoCheck(blockX, blockY);
                    if (center == null)
                        continue;
                    
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX, blockY + 1, edges);
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX + 1, blockY + 1, edges);
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX + 1, blockY, edges);
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX + 1, blockY - 1, edges);
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX, blockY - 1, edges);
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX - 1, blockY - 1, edges);
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX - 1, blockY, edges);
                    CollectEdgesBetweenFloorLists(blockX, blockY, center, blockX - 1, blockY + 1, edges);
                }
            }
            return edges;
        }

        private void CollectEdgesBetweenFloorLists(int bx, int by, 
            List<NavMeshBuilderFloorDesc> center, int nx, int ny, 
            Dictionary<EdgeVertex, List<EdgeVertex>> edgesOut)
        {
            List<NavMeshBuilderFloorDesc> neighbor = GetFloorDescListNoCheck(nx, ny);

            if (neighbor == null)
                return;

            var directionToNeighbor = NavMeshUtil.DetermineDirection(bx, by, nx, ny);
            var directionFromNeighbor = NavMeshUtil.GetInverseDirection(directionToNeighbor);


            foreach (var floor in center)
            {
                // check if direction to neighbor is blocked.
                if ((floor.DirectionFlags & directionToNeighbor) != 0)
                    continue;

                List<EdgeVertex> list = null;
                foreach (var neighborFloor in neighbor)
                {
                    // check if direction from neighbor is blocked.
                    if ((neighborFloor.DirectionFlags & directionFromNeighbor) != 0)
                        continue;

                    if (Math.Abs(floor.Z100i - neighborFloor.Z100i) / 100.0f > m_maxZStep)
                        continue;

                    if (list == null)
                    {
                        var p0 = new EdgeVertex { BX = (ushort)bx, BY = (ushort)by, Z100i = floor.Z100i };
                        if (!edgesOut.TryGetValue(p0, out list))
                        {
                            list = new List<EdgeVertex>();
                            edgesOut.Add(p0, list);
                        }
                    }

                    list.Add(new EdgeVertex { BX = (ushort)nx, BY = (ushort)ny, Z100i = neighborFloor.Z100i });

                    break; // each floor connects to 0 or 1 neighbor in 1 direction.
                }
            }
        }
        
        private void AddFloorPoint(float x, float y, float z)
        {
            int i = GetBlockIndexFromWorld(x, y);

            if (m_floorData[i] == null)
                m_floorData[i] = new List<NavMeshBuilderFloorDesc>();

            m_floorData[i].Add(new NavMeshBuilderFloorDesc((int)(z * 100)));
        }

        private int GetBlockIndexFromWorld(float x, float y)
        {
            // offset the input by half a step to center over block.
            float halfStep = m_step / 2;

            int px = (int)((x + halfStep - m_x1) / m_step);
            int py = (int)((y + halfStep - m_y1) / m_step);

            int i = py * m_blockWidth + px;
            if (i < 0 || i > m_floorData.Length)
                throw new ArgumentOutOfRangeException();

            return i;
        }

        private void ComputeDirectionFlags(GeoSpace geoSpace)
        {
            // run in 2 passes to avoid "collection was modified".
            for (int interleave = 0; interleave < 2; interleave++)
            {
                Parallel.For(0, m_blockHeight, new ParallelOptions { MaxDegreeOfParallelism = OPTION_PARALLEL_THREADS }, (y) =>
                {
                    if (y % 2 == interleave)
                        return;
                    
                    int i = y * m_blockWidth; // index to floor cell
                    for (int x = 0; x < m_blockWidth; x++, i++)
                    {
                        List<NavMeshBuilderFloorDesc> heights = m_floorData[i];
                        if (heights == null)
                            continue;

                        for (int f = 0; f < heights.Count; f++)
                        {
                            int z100 = heights[f].Z100i;
                            byte newFlags = GetBlockedDirections(geoSpace, x, y, z100);
                            heights[f] = new NavMeshBuilderFloorDesc(z100, newFlags);
                        }
                    }
                });
            }
        }

        // test if any of the 8 adjacent cells are blocked.
        // used by Build() - only valid after all points are added.
        private byte GetBlockedDirections(GeoSpace geoSpace, int blockX, int blockY, int z100i)
        {
            float x = m_x1 + blockX * m_step;
            float y = m_y1 + blockY * m_step;

            var me = new Vector3(x, y, z100i / 100.0f);

            byte flags = 0;

            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, -1, 0))
                flags |= NavMeshUtil.DIRECTION_LEFT;
            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, -1, 1))
                flags |= NavMeshUtil.DIRECTION_TL;
            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, 0, 1))
                flags |= NavMeshUtil.DIRECTION_TOP;
            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, 1, 1))
                flags |= NavMeshUtil.DIRECTION_TR;
            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, 1, 0))
                flags |= NavMeshUtil.DIRECTION_RIGHT;
            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, 1, -1))
                flags |= NavMeshUtil.DIRECTION_BR;
            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, 0, -1))
                flags |= NavMeshUtil.DIRECTION_BOTTOM;
            if (IsDirectionBlocked(geoSpace, me, blockX, blockY, -1, -1))
                flags |= NavMeshUtil.DIRECTION_BL;

            return flags;
        }

        private bool IsDirectionBlocked(GeoSpace geoSpace, Vector3 me, int blockX, int blockY, int dx, int dy)
        {
            if (blockX + dx < 0 || blockX + dx >= m_blockWidth ||
                blockY + dy < 0 || blockY + dy >= m_blockHeight)
                return true; // leaving bounding box not allowed

            var heights = GetFloorDescListChecked(blockX + dx, blockY + dy);
            if (heights == null)
                return true;

            bool result = true;

            foreach (var p in heights)
            {
                float dz = me.Z - (p.Z100i / 100.0f);

                if (Math.Abs(dz) > m_maxZStep)
                    continue;

                // check for floor below diagonal midpoint
                if (dx != 0 && dy != 0)
                {
                    var mid = new Vector3(me.X + dx * m_step / 2, me.Y + dy * m_step / 2, me.Z);

                    bool diagonalHasVerticalCollision = geoSpace.HasCollision(
                        new RayX(mid + new Vector3(0, 0, m_maxZStep),
                        new Vector3(0, 0, -1), 2 * m_maxZStep));
                    if (!diagonalHasVerticalCollision)
                        continue;
                }

                float length = new Vector3(dx * m_step, dy * m_step, dz).Length();

                // offset z from the floor to avoid small bumps.
                const float Z_OFFSET = 0.3f;

                bool canSeeNeighbor = !geoSpace.HasCollision(new RayX(me + new Vector3(0, 0, Z_OFFSET),
                    Vector3.Normalize(new Vector3(dx, dy, -dz)), length));

                if (!canSeeNeighbor)
                    continue; // view blocked

                bool neighborSeesMe = !geoSpace.HasCollision(new RayX(me + new Vector3(dx * m_step, dy * m_step, -dz + Z_OFFSET),
                    Vector3.Normalize(new Vector3(-dx, -dy, dz)), length));

                if (!neighborSeesMe)
                    continue; // view blocked

                // check for a sharp drop. look for a large z diff with midpoint z close to one side.
                if (Math.Abs(dz) >= 0.6f)
                {
                    int foundSum = 0;
                    for (int i = 1; i < 4; i++)
                    {
                        float highestZ = dz < 0 ? me.Z - dz : me.Z;
                        var mid = new Vector3(me.X + dx * m_step * i / 4, me.Y + dy * m_step * i / 4, highestZ);

                        bool foundStep = geoSpace.HasCollision(
                            new RayX(mid /*+ new Vector3(0, 0, 2)*/,
                            new Vector3(0, 0, -1), 0.8f * Math.Abs(dz) /* * m_maxZStep*/));
                        if (foundStep)
                            foundSum++;
                    }

                    // found sudden dropoff/wall
                    if (foundSum == 0)
                        continue;
                }

                // dupes can happen (and fail here) if the XY step size is greater than the required height.
                // can optimize by removing this check and just returning the result.
                if (!result)
                    throw new InvalidOperationException("multiple candidate steps");

                result = false;
            }

            return result;
        }
        
        private List<NavMeshBuilderFloorDesc> GetFloorDescListNoCheck(int x, int y)
        {
            if (x < 0 || y < 0 || x >= m_blockWidth || y >= m_blockHeight)
                return null;

            return m_floorData[y * m_blockWidth + x];
        }

        private List<NavMeshBuilderFloorDesc> GetFloorDescListChecked(int x, int y)
        {
            if (x < 0 || y < 0 || x >= m_blockWidth || y >= m_blockHeight)
                throw new InvalidOperationException();

            return m_floorData[y * m_blockWidth + x];
        }
    }
}
