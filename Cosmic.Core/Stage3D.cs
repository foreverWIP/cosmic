global using static Cosmic.Core.Scene3D;
using System.Drawing;

namespace Cosmic.Core;

public static class Scene3D
{
    const int VERTEXBUFFER_SIZE = (0x1000);
    const int FACEBUFFER_SIZE = (0x400);

    public enum FaceFlags
    {
        FACE_FLAG_TEXTURED_3D = 0,
        FACE_FLAG_TEXTURED_2D = 1,
        FACE_FLAG_COLOURED_3D = 2,
        FACE_FLAG_COLOURED_2D = 3,
    }

    public enum MatrixTypes
    {
        MAT_WORLD = 0,
        MAT_VIEW = 1,
        MAT_TEMP = 2,
    }

    public struct ScriptVertex
    {
        public int x;
        public int y;
        public int z;
        public int u;
        public int v;
    }

    public struct ScriptFace
    {
        public int a;
        public int b;
        public int c;
        public int d;
        public Color colour;
        public FaceFlags flags;
    }

    public struct DrawListEntry3D
    {
        public int faceID;
        public int depth;
    }

    internal static int vertexCount = 0;
    internal static int faceCount = 0;

    internal static Matrix matFinal = new();
    internal static Matrix matWorld = new();
    internal static Matrix matView = new();
    internal static Matrix matTemp = new();

    public static readonly ScriptFace[] faceBuffer = new ScriptFace[FACEBUFFER_SIZE];
    public static readonly ScriptVertex[] vertexBuffer = new ScriptVertex[VERTEXBUFFER_SIZE];
    public static readonly ScriptVertex[] vertexBufferT = new ScriptVertex[VERTEXBUFFER_SIZE];

    public static readonly DrawListEntry3D[] drawList3D = new DrawListEntry3D[FACEBUFFER_SIZE];

    internal static int projectionX = 136;
    internal static int projectionY = 160;

    public static void TransformVertexBuffer()
    {
        for (int y = 0; y < matFinal.values.GetLength(1); ++y)
        {
            for (int x = 0; x < matFinal.values.GetLength(0); ++x)
            {
                matFinal.values[y, x] = matWorld.values[y, x];
            }
        }
        MatrixMultiply(ref matFinal, ref matView);

        if (vertexCount <= 0)
            return;

        int inVertexID = 0;
        int outVertexID = 0;
        do
        {
            int vx = vertexBuffer[inVertexID].x;
            int vy = vertexBuffer[inVertexID].y;
            int vz = vertexBuffer[inVertexID].z;
            vertexBufferT[inVertexID].x = (vx * matFinal.values[0, 0] / 256) + (vy * matFinal.values[1, 0] / 256) + (vz * matFinal.values[2, 0] / 256) + matFinal.values[3, 0];
            vertexBufferT[inVertexID].y = (vx * matFinal.values[0, 1] / 256) + (vy * matFinal.values[1, 1] / 256) + (vz * matFinal.values[2, 1] / 256) + matFinal.values[3, 1];
            vertexBufferT[inVertexID++].z = (vx * matFinal.values[0, 2] / 256) + (vy * matFinal.values[1, 2] / 256) + (vz * matFinal.values[2, 2] / 256) + matFinal.values[3, 2];
        } while (++outVertexID != vertexCount);
    }

    internal static void TransformVerticies(ref Matrix matrix, int startIndex, int endIndex)
    {
        if (startIndex > endIndex)
            return;

        do
        {
            int vx = vertexBuffer[startIndex].x;
            int vy = vertexBuffer[startIndex].y;
            int vz = vertexBuffer[startIndex].z;
            vertexBuffer[startIndex].x = (vx * matrix.values[0, 0] / 256) + (vy * matrix.values[1, 0] / 256) + (vz * matrix.values[2, 0] / 256) + matrix.values[3, 0];
            vertexBuffer[startIndex].y = (vx * matrix.values[0, 1] / 256) + (vy * matrix.values[1, 1] / 256) + (vz * matrix.values[2, 1] / 256) + matrix.values[3, 1];
            vertexBuffer[startIndex].z = (vx * matrix.values[0, 2] / 256) + (vy * matrix.values[1, 2] / 256) + (vz * matrix.values[2, 2] / 256) + matrix.values[3, 2];
        } while (++startIndex < endIndex);
    }
}