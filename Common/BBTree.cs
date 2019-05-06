using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;

namespace monono2.Common
{
    // bounding box tree.
    public class BBTree<T>
    {
        private BBNode m_root = new BBNode();

        private class BBNode
        {
            public BoundingBox m_bbox;
            public List<BBNode> m_children;
            public T m_value;

            public BBNode()
            {
                m_children = new List<BBNode>();
            }

            public BBNode(BoundingBox bbox, T value)
            {
                m_bbox = bbox;
                m_value = value;
            }

            // EDAN30 Photorealistic Computer Graphics - Magnus Andersson
            // http://fileadmin.cs.lth.se/cs/Education/EDAN30/lectures/S2-bvh.pdf
            public void Split()
            {
                const int TARGET_LEAF_SIZE = 4;

                if (m_children == null || m_children.Count < TARGET_LEAF_SIZE)
                    return;

                // get centers and sort on the largest axis
                var centers = m_children.Select(o => new Tuple<BBNode, Vector3>(o, Util.GetBoundingBoxCenter(o.m_bbox)));
                
                var dimx = m_bbox.Max.X - m_bbox.Min.X;
                var dimy = m_bbox.Max.Y - m_bbox.Min.Y;
                var dimz = m_bbox.Max.Z - m_bbox.Min.Z;
                if (dimx >= dimy && dimx >= dimz)
                    centers = centers.OrderBy(o => o.Item2.X);
                else if (dimy >= dimz)
                    centers = centers.OrderBy(o => o.Item2.Y);
                else
                    centers = centers.OrderBy(o => o.Item2.Z);

                var left = new BBNode();
                var right = new BBNode();
                int i = 0;
                int mid = m_children.Count / 2;
                foreach (var n in centers)
                {
                    if (i++ < mid)
                        left.m_children.Add(n.Item1);
                    else
                        right.m_children.Add(n.Item1);
                }

                left.UpdateBBox();
                right.UpdateBBox();
                m_children.Clear();
                m_children.Add(left);
                m_children.Add(right);

                left.Split();
                right.Split();
            }

            private void UpdateBBox()
            {
                if (m_children.Count == 0) return;
                m_bbox = m_children[0].m_bbox;
                for (int i = 1; i < m_children.Count; i++)
                    m_bbox = BoundingBox.CreateMerged(m_bbox, m_children[i].m_bbox);
            }

            // return true to keep processing, false to stop.
            public bool DoActionOnIntersectingMeshes(BoundingBox test, Func<T, bool> func)
            {
                foreach (var c in m_children)
                {
                    if (c.m_bbox.Intersects(test))
                    {
                        if (c.m_children != null)
                        {
                            if (!EqualityComparer<T>.Default.Equals(m_value, default(T)))
                                throw new InvalidOperationException("should not have both value and children");

                            if (!c.DoActionOnIntersectingMeshes(test, func))
                                return false;
                        }
                        else
                        {
                            if (!func(c.m_value))
                                return false;
                        }
                    }
                }
                return true;
            }

            public void DebugPrint(int depth)
            {
                Log.WriteLine("".PadRight(depth * 2) + m_bbox);
                if (!EqualityComparer<T>.Default.Equals(m_value, default(T)))
                    Log.WriteLine("".PadRight((depth + 1) * 2) + "Value: " + m_value);

                if (m_children != null)
                    foreach (var c in m_children)
                        c.DebugPrint(depth + 1);
            }
            
            public void Validate()
            {
                foreach (var node in m_children)
                {
                    if (node.m_bbox.Min.X > node.m_bbox.Max.X ||
                        node.m_bbox.Min.Y > node.m_bbox.Max.Y ||
                        node.m_bbox.Min.Z > node.m_bbox.Max.Z)
                        throw new InvalidOperationException();

                    if (m_bbox.Contains(node.m_bbox) != ContainmentType.Contains)
                        throw new InvalidOperationException("child bbox not contained!");

                    if (node.m_children != null)
                        node.Validate();
                }
            }
        }

        public void Insert(BoundingBox bbox, T value)
        {
            if (m_root.m_children.Count == 0)
                m_root.m_bbox = bbox;
            else
                m_root.m_bbox = BoundingBox.CreateMerged(bbox, m_root.m_bbox);
            m_root.m_children.Add(new BBNode(bbox, value));
        }

        public void DoActionOnIntersectingMeshes(BoundingBox test, Func<T, bool> func)
        {
            m_root.DoActionOnIntersectingMeshes(test, func);
        }

        public void DebugPrint()
        {
            m_root.DebugPrint(0);
        }

        public void Validate()
        {
            m_root.Validate();
        }

        public void BuildTree()
        {
            m_root.Split();
        }

        public BoundingBox GetBoundingBox()
        {
            return m_root.m_bbox;
        }
    }
}
