using System;
using Microsoft.Xna.Framework;

namespace monono2.Common.Navigation
{
    public static class NavMeshUtil
    {
        public const int DIRECTION_TOP = 1 << 0;
        public const int DIRECTION_TR = 1 << 1;
        public const int DIRECTION_RIGHT = 1 << 2;
        public const int DIRECTION_BR = 1 << 3;
        public const int DIRECTION_BOTTOM = 1 << 4;
        public const int DIRECTION_BL = 1 << 5;
        public const int DIRECTION_LEFT = 1 << 6;
        public const int DIRECTION_TL = 1 << 7;

        public static Point OffsetFromDirectionFlag(int flag)
        {
            switch (flag)
            {
                case DIRECTION_TOP:
                    return new Point(0, 1);
                case DIRECTION_TR:
                    return new Point(1, 1);
                case DIRECTION_RIGHT:
                    return new Point(1, 0);
                case DIRECTION_BR:
                    return new Point(1, -1);
                case DIRECTION_BOTTOM:
                    return new Point(0, -1);
                case DIRECTION_BL:
                    return new Point(-1, -1);
                case DIRECTION_LEFT:
                    return new Point(-1, 0);
                case DIRECTION_TL:
                    return new Point(-1, 1);
                default:
                    throw new InvalidOperationException("invalid flag");
            }
        }

        public static int GetInverseDirection(int flag)
        {
            return ((flag & 0xF) << 4) | ((flag & 0xF0) >> 4);
        }

        public static int getAdjacentDirectionsMask(int a)
        {
            return 0xFF & ((a << 1) | (a >> 1) | (a << 7) | (a >> 7));
        }

        public static bool isDirectionAdjacent(int a, int b)
        {
            return (getAdjacentDirectionsMask(a) & b) != 0;
        }

        // x2,y2 should be 1 unit away.
        public static int DetermineDirection(int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;

            if (dx == -1)
            {
                if (dy == -1)
                    return DIRECTION_BL;
                else if (dy == 0)
                    return DIRECTION_LEFT;
                else if (dy == 1)
                    return DIRECTION_TL;
            }
            else if (dx == 0)
            {
                if (dy == -1)
                    return DIRECTION_BOTTOM;
                else if (dy == 1)
                    return DIRECTION_TOP;
            }
            else if (dx == 1)
            {
                if (dy == -1)
                    return DIRECTION_BR;
                else if (dy == 0)
                    return DIRECTION_RIGHT;
                else if (dy == 1)
                    return DIRECTION_TR;
            }

            throw new InvalidOperationException();
        }

        // slightly different version of determineDirection using floats.
        // gets the 8-way direction from 2 arbitrary vectors (ignoring z).
        public static int determineDirectionFromWorld(Vector3 start, Vector3 end)
        {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;

            int compass = (((int)Math.Round(Math.Atan2(dy, dx) / (2 * Math.PI / 8))) + 8) % 8;

            switch (compass)
            {
                case 0:
                    return DIRECTION_RIGHT;
                case 1:
                    return DIRECTION_TR;
                case 2:
                    return DIRECTION_TOP;
                case 3:
                    return DIRECTION_TL;
                case 4:
                    return DIRECTION_LEFT;
                case 5:
                    return DIRECTION_BL;
                case 6:
                    return DIRECTION_BOTTOM;
                case 7:
                    return DIRECTION_BR;
                default:
                    throw new InvalidOperationException("unexpected compass result");
            }
        }

        public static ushort EncodeHeight(int minz, int maxz, float z)
        {
            return (ushort)((z - minz) * ushort.MaxValue / (maxz - minz));
        }

        public static float DecodeHeight(int minz, int maxz, ushort encz)
        {
            return minz + encz * (maxz - minz) / (float)ushort.MaxValue;
        }
    }
}
