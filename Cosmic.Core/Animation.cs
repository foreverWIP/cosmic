using System;

namespace Cosmic.Core;

public static class Animation
{
    public const int SPRITEFRAME_COUNT = (0x1000);

    public enum AnimrotationFlags { ROTSTYLE_NONE, ROTSTYLE_FULL, ROTSTYLE_45DEG, ROTSTYLE_STATICFRAMES }

    public class AnimationFile
    {
        public string fileName = string.Empty;
        public int animCount;
        public int aniListOffset;
        public int hitboxListOffset;
    }

    public struct SpriteAnimation
    {
        public string name;
        public byte frameCount;
        public byte speed;
        public byte loopPoint;
        public AnimrotationFlags rotationStyle;
        public int frameListOffset;

        public SpriteAnimation()
        {
            name = string.Empty;
        }
    }

    public struct SpriteFrame
    {
        public int sprX;
        public int sprY;
        public int width;
        public int height;
        public int pivotX;
        public int pivotY;
        public int sheetID;
        public byte hitboxID;
    }

    public static AnimationFile GetDefaultAnimationRef() { return animationFileList[0]; }

    static readonly AnimationFile[] animationFileList = new AnimationFile[0x100];
    static int animationFileCount = 0;
    public static readonly SpriteFrame[] scriptFrames = new SpriteFrame[SPRITEFRAME_COUNT];
    internal static int scriptFrameCount = 0;

    public static readonly SpriteFrame[] animFrames = new SpriteFrame[SPRITEFRAME_COUNT];
    static int totalAnimFrameCount = 0;
    public static readonly SpriteAnimation[] animationList = new SpriteAnimation[0x400];
    static int totalAnimationCount = 0;

    public static void LoadAnimationFile(string filePath)
    {
        if (platform.LoadFile(filePath, out var info))
        {
            // var animFile = new AnimFile(new Kaitai.KaitaiStream(info.BaseStream));

            byte fileBuffer = 0;
            var strBuf = string.Empty;
            var sheetIDs = new int[0x18];
            sheetIDs[0] = 0;

            byte sheetCount = 0;
            sheetCount = info.ReadByte();

            // Read & load each spritesheet
            for (int s = 0; s < sheetCount; ++s)
            {
                strBuf = string.Empty;
                fileBuffer = info.ReadByte();
                if (fileBuffer != 0)
                {
                    int i = 0;
                    for (; i < fileBuffer; ++i)
                    {
                        byte b = info.ReadByte();
                        strBuf += (char)b;
                    }
                    sheetIDs[s] = Sprite.Add(strBuf);
                }
            }

            byte animCount = info.ReadByte();
            AnimationFile animFile = animationFileList[animationFileCount];
            animFile.animCount = animCount;
            animFile.aniListOffset = totalAnimationCount;

            void ReadSpriteFrame(ref SpriteFrame frame)
            {
                frame.sheetID = info.ReadByte();
                frame.sheetID = sheetIDs[frame.sheetID];
                frame.hitboxID = info.ReadByte();
                fileBuffer = info.ReadByte();
                frame.sprX = fileBuffer;
                fileBuffer = info.ReadByte();
                frame.sprY = fileBuffer;
                fileBuffer = info.ReadByte();
                frame.width = fileBuffer;
                fileBuffer = info.ReadByte();
                frame.height = fileBuffer;

                sbyte buffer = 0;
                buffer = info.ReadSByte();
                frame.pivotX = buffer;
                buffer = info.ReadSByte();
                frame.pivotY = buffer;
            }

            // Read animations
            for (int a = 0; a < animCount; ++a)
            {
                var anim = new SpriteAnimation
                {
                    frameListOffset = totalAnimFrameCount,
                    name = info.ReadPascalString(),
                    frameCount = info.ReadByte(),
                    speed = info.ReadByte(),
                    loopPoint = info.ReadByte()
                };
                byte rotB = info.ReadByte();
                anim.rotationStyle = (AnimrotationFlags)rotB;

                for (int j = 0; j < anim.frameCount; ++j)
                {
                    ReadSpriteFrame(ref animFrames[totalAnimFrameCount++]);
                }

                // 90 Degree (Extra rotation Frames) rotation
                if (anim.rotationStyle == AnimrotationFlags.ROTSTYLE_STATICFRAMES)
                    anim.frameCount >>= 1;

                animationList[totalAnimationCount++] = anim;
            }

            // Read Hitboxes
            animFile.hitboxListOffset = hitboxList.Count;
            fileBuffer = info.ReadByte();
            for (int i = 0; i < fileBuffer; ++i)
            {
                var hitbox = new Hitbox();
                for (int d = 0; d < Hitbox.DirectionCount; ++d)
                {
                    hitbox.left[d] = info.ReadSByte();
                    hitbox.top[d] = info.ReadSByte();
                    hitbox.right[d] = info.ReadSByte();
                    hitbox.bottom[d] = info.ReadSByte();
                }
                hitboxList.Add(hitbox);
            }

            info.Dispose();
        }
    }

    public static void ClearAnimationData()
    {
        Array.Clear(scriptFrames);
        Array.Clear(animFrames);
        hitboxList.Clear();
        Array.Clear(animationList);
        Array.Clear(animationFileList);

        scriptFrameCount = 0;
        totalAnimFrameCount = 0;
        totalAnimationCount = 0;
        animationFileCount = 0;
    }

    public static AnimationFile? AddAnimationFile(string filePath)
    {
        var path = "Data/Animations/";
        path += filePath;

        // If matching anim is found return that, otherwise load a new anim
        for (int a = 0; a < animationFileList.Length; ++a)
        {
            animationFileList[a] ??= new();
            if ((animationFileList[a].fileName ?? string.Empty).Length <= 0)
            {
                LoadAnimationFile(path);
                ++animationFileCount;
                return animationFileList[a];
            }
            if (animationFileList[a].fileName == filePath)
                return animationFileList[a];
        }
        return null;
    }

    public static void ProcessObjectAnimation(ref ObjectScript objectScript, ref Entity entity)
    {
        SpriteAnimation sprAnim = animationList[objectScript.animFile.aniListOffset + entity.animation];

        if (entity.animationSpeed <= 0)
        {
            entity.animationTimer += sprAnim.speed;
        }
        else
        {
            entity.animationSpeed = System.Math.Min(entity.animationSpeed, 0xf0);
            entity.animationTimer += entity.animationSpeed;
        }

        if (entity.animation != entity.prevAnimation)
        {
            entity.prevAnimation = entity.animation;
            entity.frame = 0;
            entity.animationTimer = 0;
            entity.animationSpeed = 0;
        }

        if (entity.animationTimer >= 0xF0)
        {
            entity.animationTimer -= 0xF0;
            ++entity.frame;
        }

        if (entity.frame >= sprAnim.frameCount)
            entity.frame = sprAnim.loopPoint;
    }
}