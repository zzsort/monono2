using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using monono2.Common.Navigation;

namespace monono2.AionMonoLib
{
    public class AstarVertexBufferGenerator
    {
        private GraphicsDevice GraphicsDevice;
        private CompiledNavMeshSet m_compiledNavMeshSet;
        private Vector3 m_testNavTarget = new Vector3(1900.072f, 2591.614f, 264.4005f);
        private Vector3 m_testNavStart = new Vector3(1874.048f, 2618.841f, 289.2002f);

        public AstarVertexBufferGenerator(GraphicsDevice GraphicsDevice, CompiledNavMeshSet compiledNavMeshSet)
        {
            this.GraphicsDevice = GraphicsDevice;
            m_compiledNavMeshSet = compiledNavMeshSet;
        }

        public void SetTargetPoint(Vector3 target, RenderData renderData, ref string resultMessage)
        {
            if (m_compiledNavMeshSet == null)
                return;
            if (m_testNavTarget == target)
                return;
            m_testNavTarget = target;
            var sg = m_compiledNavMeshSet.FindSubgraphUnderPoint(target.X, target.Y, target.Z + 4, 10);

            //   m_testString = $" TEST[pt: {c}  {(sg==null?"MESH NOT FOUND":$"{sg.BlockWidth} {sg.BlockHeight} {sg.Z1} {sg.Z2}")}]";
            renderData.astarLineVertexBuffer = CreateVertexBuffer(out resultMessage);
        }

        public void SetStartPoint(Vector3 target, RenderData renderData, ref string resultMessage)
        {
            if (m_compiledNavMeshSet == null)
                return;
            if (m_testNavStart == target)
                return;
            m_testNavStart = target;
            renderData.astarLineVertexBuffer = CreateVertexBuffer(out resultMessage);
        }

        private VertexBuffer CreateVertexBuffer(out string resultMessage)
        {
            resultMessage = "";
            if (m_testNavTarget == Vector3.Zero || m_testNavStart == Vector3.Zero)
                return null;

            int maxFall = 30;
            var startMesh = m_compiledNavMeshSet.FindSubgraphUnderPoint(m_testNavStart.X, m_testNavStart.Y, m_testNavStart.Z, maxFall);
            var goalMesh = m_compiledNavMeshSet.FindSubgraphUnderPoint(m_testNavTarget.X, m_testNavTarget.Y, m_testNavTarget.Z, maxFall);

            if (startMesh == null || goalMesh == null)
            {
                resultMessage = $"start or goal is null: start:{startMesh} goal:{goalMesh}";
                return null;
            }
            if (startMesh != goalMesh)
            {
                resultMessage = $"start and goal are different meshes";
                return null;
            }

            var lines = TestAStar(startMesh, m_testNavStart, m_testNavTarget, maxFall);
            if (lines == null || lines.Count == 0)
                return null;

            var buffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), lines.Count, BufferUsage.WriteOnly);
            buffer.SetData(lines.ToArray());
            return buffer;
        }

        private List<VertexPositionColor> TestAStar(CompiledNavMesh compiledMesh, Vector3 startVec, Vector3 endVec, int maxFall)
        {
            var startNode = compiledMesh.FindFloorUnderPoint(startVec.X, startVec.Y, startVec.Z + 2, maxFall);
            var goalNode = compiledMesh.FindFloorUnderPoint(endVec.X, endVec.Y, endVec.Z + 2, maxFall);
            if (startNode.blockIndex == goalNode.blockIndex)
                return null;

            var exactEnd = endVec; // TODO - caller should provide this
            var start = compiledMesh.WorldPointFromNode(startNode);
            var goal = compiledMesh.WorldPointFromNode(goalNode);

            var floorLineVertices = new List<VertexPositionColor>();
            floorLineVertices.Add(new VertexPositionColor(start + new Vector3(0, 0, 5), Color.Lime));
            floorLineVertices.Add(new VertexPositionColor(start, Color.Lime));

            floorLineVertices.Add(new VertexPositionColor(goal + new Vector3(0, 0, 5), Color.Red));
            floorLineVertices.Add(new VertexPositionColor(goal, Color.Red));

            var path = compiledMesh.AStar(startNode, goalNode);
            var currentNode = goalNode;
            while (true)
            {
                if (!path.ContainsKey(currentNode))
                    throw new InvalidOperationException();
                var next = path[currentNode]; // walks backwards from goal, so really 'prev'
                if (next.Equals(default(CompiledNavMesh.NodeId)))
                    break;

                var ep0 = compiledMesh.WorldPointFromNode(currentNode);
                var ep1 = compiledMesh.WorldPointFromNode(next);

                floorLineVertices.Add(new VertexPositionColor(ep0 + new Vector3(0, 0, 0.1f), Color.White));
                floorLineVertices.Add(new VertexPositionColor(ep1 + new Vector3(0, 0, 0.1f), Color.White));

                currentNode = next;
            }

            // overlay the reduced path on top of original AStar results...

            // findPath runs AStar again but with path point reduction
            var pathPointList = compiledMesh.findPath(startNode, goalNode, exactEnd);

            for (int i = 1; i < pathPointList.Count; i++)
            {
                floorLineVertices.Add(new VertexPositionColor(pathPointList[i] + new Vector3(0, 0, 0.2f), Color.Blue));
                floorLineVertices.Add(new VertexPositionColor(pathPointList[i - 1] + new Vector3(0, 0, 0.2f), Color.Blue));
            }
            return floorLineVertices;
        }
    }
}
