using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace monono2.Common.Navigation
{
    // manages whole-world collision testing
    public class GeoSpace
    {
        private BBTree<Vector3[]> m_collisionMeshTree = new BBTree<Vector3[]>();
        
        // add a subset of an array as a mesh.
        public void AddCollidableMeshToTree(List<VertexPositionColor> collisionVertices,
            int collisionStart, int count)
        {
            int collisionEnd = collisionStart + count; //collisionVertices.Count;
            if (collisionStart != collisionEnd)
            {
                // break large meshes into triangles to improve AABB tree.
                // TODO - also split meshes with large areas?
                if (collisionEnd - collisionStart > 8000)
                {
                    // add individual triangles from mesh
                    for (int i = collisionStart; i < collisionEnd;)
                    {
                        var points = new Vector3[3];
                        points[0] = collisionVertices[i++].Position;
                        points[1] = collisionVertices[i++].Position;
                        points[2] = collisionVertices[i++].Position;
                        m_collisionMeshTree.Insert(BoundingBox.CreateFromPoints(points), points);
                    }
                    return;
                }
                {
                    // Add collidable mesh to bb tree
                    var points = new Vector3[collisionEnd - collisionStart];
                    for (int i = collisionStart, dst = 0; i < collisionEnd; i++, dst++)
                        points[dst] = collisionVertices[i].Position;
                    m_collisionMeshTree.Insert(BoundingBox.CreateFromPoints(points), points);
                }
            }
        }

        public void AddCollidableMeshToTree(List<Vector3> triangles)
        {
            if (triangles.Count > 0)
            {
                m_collisionMeshTree.Insert(BoundingBox.CreateFromPoints(triangles), triangles.ToArray());
            }
        }
        
        public bool HasCollision(RayX ray)
        {
            bool hasCollision = false;
            m_collisionMeshTree.DoActionOnIntersectingMeshes(ray.GetBoundingBox(),
                (Vector3[] points) =>
                {
                    for (int i = 0; i < points.Length; i += 3)
                    {
                        float t = ray.IntersectsTriangle(points[i], points[i + 1], points[i + 2]);
                        if (t <= ray.Limit)
                        {
                            hasCollision = true;
                            return false; // stop
                        }
                    }
                    return true;
                });
            return hasCollision;
        }

        public void DoActionOnIntersectingMeshes(BoundingBox test, Func<Vector3[], bool> func)
        {
            m_collisionMeshTree.DoActionOnIntersectingMeshes(test, func);
        }

        public void BuildTree()
        {
            m_collisionMeshTree.BuildTree();
        }

        public void Validate()
        {
            m_collisionMeshTree.Validate();

            //m_collisionMeshTree.DebugPrint();
        }

        public BoundingBox GetBoundingBox()
        {
            return m_collisionMeshTree.GetBoundingBox();
        }
    }
}
