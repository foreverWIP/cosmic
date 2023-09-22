global using static Cosmic.Core.Text;

namespace Cosmic.Core;

public static class Text
{
    public enum TextInfoTypes { TEXTINFO_TEXTDATA = 0, TEXTINFO_TEXTSIZE = 1, TEXTINFO_ROWCOUNT = 2 }

    public struct TextMenu
    {
        public const int TEXTDATA_COUNT = 0x2800;
        public const int TEXTENTRY_COUNT = 0x200;

        public ushort[] textData
        {
            get
            {
                return _textData ??= new ushort[TEXTDATA_COUNT];
            }
        }
        ushort[] _textData;
        public int[] entryStart
        {
            get
            {
                return _entryStart ??= new int[TEXTENTRY_COUNT];
            }
        }
        int[] _entryStart;
        public int[] entrySize
        {
            get
            {
                return _entrySize ??= new int[TEXTENTRY_COUNT];
            }
        }
        int[] _entrySize;
        public byte[] entryHighlight
        {
            get
            {
                return _entryHighlight ??= new byte[TEXTENTRY_COUNT];
            }
        }
        byte[] _entryHighlight;
        public int textDataPos;
        public int selection1;
        public ushort rowCount;
        public byte alignment;
        public byte selectionCount;
    }

    public struct FontCharacter
    {
        public int id;
        public short srcX;
        public short srcY;
        public short width;
        public short height;
        public short pivotX;
        public short pivotY;
        public short xAdvance;
    }

    public static readonly TextMenu[] gameMenu = new TextMenu[0x2];
    internal static int textMenuSurfaceNo = 0;

    public static readonly FontCharacter[] fontCharacterList = new FontCharacter[0x400];

    public static void LoadFontFile(string filePath)
    {
        byte fileBuffer;
        int cnt = 0;
        if (platform.LoadFile(filePath, out var info))
        {
            // + 20 for chardef byte size
            while ((info.BaseStream.Position + 20) < info.BaseStream.Length)
            {
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].id = fileBuffer;
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].id += fileBuffer << 8;
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].id += fileBuffer << 16;
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].id += fileBuffer << 24;

                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].srcX = fileBuffer;
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].srcX += (short)(fileBuffer << 8);

                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].srcY = fileBuffer;
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].srcY += (short)(fileBuffer << 8);

                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].width = (short)(fileBuffer + 1);
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].width += (short)(fileBuffer << 8);

                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].height = (short)(fileBuffer + 1);
                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].height += (short)(fileBuffer << 8);

                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].pivotX = fileBuffer;
                fileBuffer = info.ReadByte();
                if (fileBuffer > 0x80)
                {
                    fontCharacterList[cnt].pivotX += (short)((fileBuffer - 0x80) << 8);
                    fontCharacterList[cnt].pivotX += -0x8000;
                }
                else
                {
                    fontCharacterList[cnt].pivotX += (short)(fileBuffer << 8);
                }

                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].pivotY = fileBuffer;
                fileBuffer = info.ReadByte();
                if (fileBuffer > 0x80)
                {
                    fontCharacterList[cnt].pivotY += (short)((fileBuffer - 0x80) << 8);
                    fontCharacterList[cnt].pivotY += -0x8000;
                }
                else
                {
                    fontCharacterList[cnt].pivotY += (short)(fileBuffer << 8);
                }

                fileBuffer = info.ReadByte();
                fontCharacterList[cnt].xAdvance = fileBuffer;
                fileBuffer = info.ReadByte();
                if (fileBuffer > 0x80)
                {
                    fontCharacterList[cnt].xAdvance += (short)((fileBuffer - 0x80) << 8);
                    fontCharacterList[cnt].xAdvance += -0x8000;
                }
                else
                {
                    fontCharacterList[cnt].xAdvance += (short)(fileBuffer << 8);
                }

                // Unused
                _ = info.ReadByte();
                _ = info.ReadByte();
                cnt++;
            }
            info.Dispose();
        }
    }

    public static void LoadTextFile(ref TextMenu menu, string filePath, byte mapCode)
    {
        bool finished = false;
        byte fileBuffer;
        if (platform.LoadFile(filePath, out var info))
        {
            if (menu.entryStart == null)
            {
                menu = new();
            }
            menu.textDataPos = 0;
            menu.rowCount = 0;
            menu.entryStart[menu.rowCount] = menu.textDataPos;
            menu.entrySize[menu.rowCount] = 0;

            fileBuffer = info.ReadByte();
            if (fileBuffer == 0xFF)
            {
                _ = info.ReadByte();
                while (!finished)
                {
                    ushort character;
                    fileBuffer = info.ReadByte();
                    character = fileBuffer;
                    fileBuffer = info.ReadByte();
                    character |= (ushort)(fileBuffer << 8);

                    if (character != '\n')
                    {
                        if (character == '\r')
                        {
                            menu.rowCount++;
                            if (menu.rowCount > 511)
                            {
                                finished = true;
                            }
                            else
                            {
                                menu.entryStart[menu.rowCount] = menu.textDataPos;
                                menu.entrySize[menu.rowCount] = 0;
                            }
                        }
                        else
                        {
                            if (mapCode != 0)
                            {
                                int i = 0;
                                while (i < 1024)
                                {
                                    if (fontCharacterList[i].id == character)
                                    {
                                        character = (ushort)i;
                                        i = 1025;
                                    }
                                    else
                                    {
                                        ++i;
                                    }
                                }
                                if (i == 1024)
                                {
                                    character = 0;
                                }
                            }
                            menu.textData[menu.textDataPos++] = character;
                            menu.entrySize[menu.rowCount]++;
                        }
                    }
                    if (!finished)
                    {
                        finished = info.BaseStream.Position >= info.BaseStream.Length;
                        if (menu.textDataPos >= TextMenu.TEXTDATA_COUNT)
                            finished = true;
                    }
                }
            }
            else
            {
                ushort character = fileBuffer;
                if (character != '\n')
                {
                    if (character == '\r')
                    {
                        menu.rowCount++;
                        menu.entryStart[menu.rowCount] = menu.textDataPos;
                        menu.entrySize[menu.rowCount] = 0;
                    }
                    else
                    {
                        if (mapCode != 0)
                        {
                            int i = 0;
                            while (i < 1024)
                            {
                                if (fontCharacterList[i].id == character)
                                {
                                    character = (ushort)i;
                                    i = 1025;
                                }
                                else
                                {
                                    ++i;
                                }
                            }
                            if (i == 1024)
                            {
                                character = 0;
                            }
                        }
                        menu.textData[menu.textDataPos++] = character;
                        menu.entrySize[menu.rowCount]++;
                    }
                }

                while (!finished)
                {
                    fileBuffer = info.ReadByte();
                    character = fileBuffer;
                    if (character != '\n')
                    {
                        if (character == '\r')
                        {
                            menu.rowCount++;
                            if (menu.rowCount > 511)
                            {
                                finished = true;
                            }
                            else
                            {
                                menu.entryStart[menu.rowCount] = menu.textDataPos;
                                menu.entrySize[menu.rowCount] = 0;
                            }
                        }
                        else
                        {
                            if (mapCode != 0)
                            {
                                int i = 0;
                                while (i < 1024)
                                {
                                    if (fontCharacterList[i].id == character)
                                    {
                                        character = (ushort)i;
                                        i = 1025;
                                    }
                                    else
                                    {
                                        ++i;
                                    }
                                }
                                if (i == 1024)
                                    character = 0;
                            }
                            menu.textData[menu.textDataPos++] = character;
                            menu.entrySize[menu.rowCount]++;
                        }
                    }
                    if (!finished)
                    {
                        finished = info.BaseStream.Position >= info.BaseStream.Length;
                        if (menu.textDataPos >= TextMenu.TEXTDATA_COUNT)
                            finished = true;
                    }
                }
            }
            menu.rowCount++;
            info.Dispose();
        }
    }

    public static void SetupTextMenu(ref TextMenu menu, int rowCount)
    {
        menu.textDataPos = 0;
        menu.rowCount = (ushort)rowCount;
    }

    public static void AddTextMenuEntry(ref TextMenu menu, string text)
    {
        menu.entryStart[menu.rowCount] = menu.textDataPos;
        menu.entrySize[menu.rowCount] = 0;
        menu.entryHighlight[menu.rowCount] = 0;
        for (int i = 0; i < text.Length;)
        {
            if (text[i] != '\0')
            {
                menu.textData[menu.textDataPos++] = text[i];
                menu.entrySize[menu.rowCount]++;
                ++i;
            }
            else
            {
                break;
            }
        }
        menu.rowCount++;
    }
}