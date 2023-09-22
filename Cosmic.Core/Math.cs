global using static Cosmic.Core.Math;

namespace Cosmic.Core;

static unsafe class Math
{
    public readonly struct Matrix
    {
        public readonly int[,] values;

        public Matrix()
        {
            values = new int[4, 4];
        }
    }

    public static int DegreesToByteAngle(float degrees)
    {
        return (int)(((degrees % 360) / 360) * 256);
    }

    public static int WholeToFixedPoint(int units) => units << 16;
    public static int FixedPointToWhole(int value) => value >> 16;
    public static int PositiveClampedModuloInclusive(int value, int by)
    {
        if (value < 0)
        {
            value += by;
        }
        if (value >= by)
        {
            value -= by;
        }
        return value;
    }

    public static int Sin512(int angle)
    {
        if (angle < 0)
            angle = 0x200 - angle;
        angle &= 0x1FF;
        return sin512LookupTable[angle];
    }

    public static int Cos512(int angle)
    {
        if (angle < 0)
            angle = 0x200 - angle;
        angle &= 0x1FF;
        return cos512LookupTable[angle];
    }

    public static int Sin256(int angle)
    {
        if (angle < 0)
            angle = 0x100 - angle;
        angle &= 0xFF;
        return sin256LookupTable[angle];
    }

    public static int Cos256(int angle)
    {
        if (angle < 0)
            angle = 0x100 - angle;
        angle &= 0xFF;
        return cos256LookupTable[angle];
    }

    public static readonly int[] sinMLookupTable = new int[512];
    public static readonly int[] cosMLookupTable = new int[512];

    public static readonly int[] sin512LookupTable = new int[512];
    public static readonly int[] cos512LookupTable = new int[512];

    public static readonly int[] sin256LookupTable = new int[256];
    public static readonly int[] cos256LookupTable = new int[256];

    static readonly byte[] arcTan256LookupTable = new byte[0x100 * 0x100];

    // Setup Angles
    static Math()
    {
        for (int i = 0; i < 0x200; ++i)
        {
            sinMLookupTable[i] = (int)(System.Math.Sin((i / 256.0) * System.Math.PI) * 4096.0);
            cosMLookupTable[i] = (int)(System.Math.Cos((i / 256.0) * System.Math.PI) * 4096.0);
        }

        cosMLookupTable[0x00] = 0x1000;
        cosMLookupTable[0x80] = 0;
        cosMLookupTable[0x100] = -0x1000;
        cosMLookupTable[0x180] = 0;

        sinMLookupTable[0x00] = 0;
        sinMLookupTable[0x80] = 0x1000;
        sinMLookupTable[0x100] = 0;
        sinMLookupTable[0x180] = -0x1000;

        for (int i = 0; i < 0x200; ++i)
        {
            sin512LookupTable[i] = (int)(System.MathF.Sin((i / 256.0f) * System.MathF.PI) * 512.0f);
            cos512LookupTable[i] = (int)(System.MathF.Cos((i / 256.0f) * System.MathF.PI) * 512.0f);
        }

        cos512LookupTable[0x00] = 0x200;
        cos512LookupTable[0x80] = 0;
        cos512LookupTable[0x100] = -0x200;
        cos512LookupTable[0x180] = 0;

        sin512LookupTable[0x00] = 0;
        sin512LookupTable[0x80] = 0x200;
        sin512LookupTable[0x100] = 0;
        sin512LookupTable[0x180] = -0x200;

        for (int i = 0; i < 0x100; i++)
        {
            sin256LookupTable[i] = (sin512LookupTable[i * 2] >> 1);
            cos256LookupTable[i] = (cos512LookupTable[i * 2] >> 1);
        }

        for (int Y = 0; Y < 0x100; ++Y)
        {
            fixed (byte* atanPtr = &arcTan256LookupTable[Y])
            {
                byte* atan = atanPtr;
                for (int X = 0; X < 0x100; ++X)
                {
                    float angle = System.MathF.Atan2(Y, X);
                    *atan = (byte)(angle * (256 / (System.MathF.PI * 2)));
                    atan += 0x100;
                }
            }
        }
    }

    public static byte ArcTanLookup(int X, int Y)
    {
        int x = System.Math.Abs(X);
        int y = System.Math.Abs(Y);

        if (x <= y)
        {
            while (y > 0xFF)
            {
                x >>= 4;
                y >>= 4;
            }
        }
        else
        {
            while (x > 0xFF)
            {
                x >>= 4;
                y >>= 4;
            }
        }
        if (X <= 0)
        {
            if (Y <= 0)
            {
                return (byte)(arcTan256LookupTable[(x << 8) + y] + -0x80);
            }
            else
            {
                return (byte)(-0x80 - arcTan256LookupTable[(x << 8) + y]);
            }
        }
        else if (Y <= 0)
        {
            return (byte)-arcTan256LookupTable[(x << 8) + y];
        }
        else
        {
            return arcTan256LookupTable[(x << 8) + y];
        }
    }

    public static void SetIdentityMatrix(ref Matrix matrix)
    {
        matrix.values[0, 0] = 0x100;
        matrix.values[0, 1] = 0;
        matrix.values[0, 2] = 0;
        matrix.values[0, 3] = 0;
        matrix.values[1, 0] = 0;
        matrix.values[1, 1] = 0x100;
        matrix.values[1, 2] = 0;
        matrix.values[1, 3] = 0;
        matrix.values[2, 0] = 0;
        matrix.values[2, 1] = 0;
        matrix.values[2, 2] = 0x100;
        matrix.values[2, 3] = 0;
        matrix.values[3, 0] = 0;
        matrix.values[3, 0] = 0;
        matrix.values[3, 1] = 0;
        matrix.values[3, 2] = 0;
        matrix.values[3, 3] = 0x100;
    }

    public static void MatrixMultiply(ref Matrix matrixA, ref Matrix matrixB)
    {
        var output = new int[16];

        for (int i = 0; i < 0x10; ++i)
        {
            uint RowB = (uint)(i & 3);
            uint RowA = (uint)((i & 0xC) / 4);
            output[i] = (matrixA.values[RowA, 3] * matrixB.values[3, RowB] >> 8) + (matrixA.values[RowA, 2] * matrixB.values[2, RowB] >> 8)
                        + (matrixA.values[RowA, 1] * matrixB.values[1, RowB] >> 8) + (matrixA.values[RowA, 0] * matrixB.values[0, RowB] >> 8);
        }

        for (int i = 0; i < 0x10; ++i) matrixA.values[i / 4, i % 4] = output[i];
    }

    public static void MatrixTranslateXYZ(ref Matrix matrix, int x, int y, int z)
    {
        matrix.values[0, 0] = 0x100;
        matrix.values[0, 1] = 0;
        matrix.values[0, 2] = 0;
        matrix.values[0, 3] = 0;
        matrix.values[1, 0] = 0;
        matrix.values[1, 1] = 0x100;
        matrix.values[1, 2] = 0;
        matrix.values[1, 3] = 0;
        matrix.values[2, 0] = 0;
        matrix.values[2, 1] = 0;
        matrix.values[2, 2] = 0x100;
        matrix.values[2, 3] = 0;
        matrix.values[3, 0] = x;
        matrix.values[3, 1] = y;
        matrix.values[3, 2] = z;
        matrix.values[3, 3] = 0x100;
    }

    public static void MatrixScaleXYZ(ref Matrix matrix, int scaleX, int scaleY, int scaleZ)
    {
        matrix.values[0, 0] = scaleX;
        matrix.values[0, 1] = 0;
        matrix.values[0, 2] = 0;
        matrix.values[0, 3] = 0;
        matrix.values[1, 0] = 0;
        matrix.values[1, 1] = scaleY;
        matrix.values[1, 2] = 0;
        matrix.values[1, 3] = 0;
        matrix.values[2, 0] = 0;
        matrix.values[2, 1] = 0;
        matrix.values[2, 2] = scaleZ;
        matrix.values[2, 3] = 0;
        matrix.values[3, 0] = 0;
        matrix.values[3, 1] = 0;
        matrix.values[3, 2] = 0;
        matrix.values[3, 3] = 0x100;
    }

    public static void MatrixRotateX(ref Matrix matrix, int rotationX)
    {
        if (rotationX < 0)
            rotationX = 0x200 - rotationX;
        rotationX &= 0x1FF;
        int sine = sin512LookupTable[rotationX] >> 1;
        int cosine = cos512LookupTable[rotationX] >> 1;
        matrix.values[0, 0] = 0x100;
        matrix.values[0, 1] = 0;
        matrix.values[0, 2] = 0;
        matrix.values[0, 3] = 0;
        matrix.values[1, 0] = 0;
        matrix.values[1, 1] = cosine;
        matrix.values[1, 2] = sine;
        matrix.values[1, 3] = 0;
        matrix.values[2, 0] = 0;
        matrix.values[2, 1] = -sine;
        matrix.values[2, 2] = cosine;
        matrix.values[2, 3] = 0;
        matrix.values[3, 0] = 0;
        matrix.values[3, 1] = 0;
        matrix.values[3, 2] = 0;
        matrix.values[3, 3] = 0x100;
    }

    public static void MatrixRotateY(ref Matrix matrix, int rotationY)
    {
        if (rotationY < 0)
            rotationY = 0x200 - rotationY;
        rotationY &= 0x1FF;
        int sine = sin512LookupTable[rotationY] >> 1;
        int cosine = cos512LookupTable[rotationY] >> 1;
        matrix.values[0, 0] = cosine;
        matrix.values[0, 1] = 0;
        matrix.values[0, 2] = sine;
        matrix.values[0, 3] = 0;
        matrix.values[1, 0] = 0;
        matrix.values[1, 1] = 0x100;
        matrix.values[1, 2] = 0;
        matrix.values[1, 3] = 0;
        matrix.values[2, 0] = -sine;
        matrix.values[2, 1] = 0;
        matrix.values[2, 2] = cosine;
        matrix.values[2, 3] = 0;
        matrix.values[3, 0] = 0;
        matrix.values[3, 1] = 0;
        matrix.values[3, 2] = 0;
        matrix.values[3, 3] = 0x100;
    }

    public static void MatrixRotateZ(ref Matrix matrix, int rotationZ)
    {
        if (rotationZ < 0)
            rotationZ = 0x200 - rotationZ;
        rotationZ &= 0x1FF;
        int sine = sin512LookupTable[rotationZ] >> 1;
        int cosine = cos512LookupTable[rotationZ] >> 1;
        matrix.values[0, 0] = cosine;
        matrix.values[0, 1] = 0;
        matrix.values[0, 2] = sine;
        matrix.values[0, 3] = 0;
        matrix.values[1, 0] = 0;
        matrix.values[1, 1] = 0x100;
        matrix.values[1, 2] = 0;
        matrix.values[1, 3] = 0;
        matrix.values[2, 0] = -sine;
        matrix.values[2, 1] = 0;
        matrix.values[2, 2] = cosine;
        matrix.values[2, 3] = 0;
        matrix.values[3, 0] = 0;
        matrix.values[3, 1] = 0;
        matrix.values[3, 2] = 0;
        matrix.values[3, 3] = 0x100;
    }

    public static void MatrixRotateXYZ(ref Matrix matrix, int rotationX, int rotationY, int rotationZ)
    {
        if (rotationX < 0)
            rotationX = 0x200 - rotationX;
        rotationX &= 0x1FF;
        if (rotationY < 0)
            rotationY = 0x200 - rotationY;
        rotationY &= 0x1FF;
        if (rotationZ < 0)
            rotationZ = 0x200 - rotationZ;
        rotationZ &= 0x1FF;
        int sineX = sin512LookupTable[rotationX] >> 1;
        int cosineX = cos512LookupTable[rotationX] >> 1;
        int sineY = sin512LookupTable[rotationY] >> 1;
        int cosineY = cos512LookupTable[rotationY] >> 1;
        int sineZ = sin512LookupTable[rotationZ] >> 1;
        int cosineZ = cos512LookupTable[rotationZ] >> 1;

        matrix.values[0, 0] = (sineZ * (sineY * sineX >> 8) >> 8) + (cosineZ * cosineY >> 8);
        matrix.values[0, 1] = (sineZ * cosineY >> 8) - (cosineZ * (sineY * sineX >> 8) >> 8);
        matrix.values[0, 2] = sineY * cosineX >> 8;
        matrix.values[0, 3] = 0;
        matrix.values[1, 0] = sineZ * -cosineX >> 8;
        matrix.values[1, 1] = cosineZ * cosineX >> 8;
        matrix.values[1, 2] = sineX;
        matrix.values[1, 3] = 0;
        matrix.values[2, 0] = (sineZ * (cosineY * sineX >> 8) >> 8) - (cosineZ * sineY >> 8);
        matrix.values[2, 1] = (sineZ * -sineY >> 8) - (cosineZ * (cosineY * sineX >> 8) >> 8);
        matrix.values[2, 2] = cosineY * cosineX >> 8;
        matrix.values[2, 3] = 0;
        matrix.values[3, 0] = 0;
        matrix.values[3, 1] = 0;
        matrix.values[3, 2] = 0;
        matrix.values[3, 3] = 0x100;
    }
}