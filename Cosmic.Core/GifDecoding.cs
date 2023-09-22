using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Cosmic.Formats;
using static Cosmic.Formats.Gif;

namespace Cosmic;

static class GifDecoder
{
    static void InitDictionary(Dictionary<int, StringBuilder> dic, int lzwMinimumCodeSize, out int lzwCodeSize, out int clearCode, out int finishCode)
    {
        int dicLength = (int)Math.Pow(2, lzwMinimumCodeSize);

        clearCode = dicLength;
        finishCode = clearCode + 1;

        dic.Clear();

        for (int i = 0; i < dicLength + 2; i++)
        {
            dic.Add(i, new StringBuilder(((char)i).ToString(), 512));
        }

        lzwCodeSize = lzwMinimumCodeSize + 1;
    }

    static int GetNumeral(this BitArray array, int startIndex, int bitLength)
    {
        var asInt = 0;

        for (int i = 0; i < bitLength; i++)
        {
            bool bit = array[startIndex + i];
            asInt |= (bit ? 1 : 0) << i;
        }

        return asInt;
    }

    private static byte[] SortInterlaceGifData(byte[] decodedData, int xNum)
    {
        int rowNo = 0;
        int dataIndex = 0;
        var newArr = new byte[decodedData.Length];
        // Every 8th. row, starting with row 0.
        for (int i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 8 == 0)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }
            if (i != 0 && i % xNum == 0)
            {
                rowNo++;
            }
        }
        rowNo = 0;
        // Every 8th. row, starting with row 4.
        for (int i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 8 == 4)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }
            if (i != 0 && i % xNum == 0)
            {
                rowNo++;
            }
        }
        rowNo = 0;
        // Every 4th. row, starting with row 2.
        for (int i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 4 == 2)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }
            if (i != 0 && i % xNum == 0)
            {
                rowNo++;
            }
        }
        rowNo = 0;
        // Every 2nd. row, starting with row 1.
        for (int i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 8 != 0 && rowNo % 8 != 4 && rowNo % 4 != 2)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }
            if (i != 0 && i % xNum == 0)
            {
                rowNo++;
            }
        }

        return newArr;
    }

    private static byte[] DecodeGifLZW(byte[] compData, int lzwMinimumCodeSize, int needDataSize)
    {
        // Initialize dictionary
        var dic = new Dictionary<int, StringBuilder>(128);
        InitDictionary(dic, lzwMinimumCodeSize, out var lzwCodeSize, out var clearCode, out var finishCode);

        // Convert to bit array
        byte[] compDataArr = compData;
        var bitData = new BitArray(compDataArr);

        byte[] output = new byte[needDataSize];
        int outputAddIndex = 0;

        var entry = new StringBuilder();
        string? prevEntry = null;

        bool dicInitFlag = false;

        int bitDataIndex = 0;

        // LZW decode loop
        while (bitDataIndex < bitData.Length)
        {
            if (dicInitFlag)
            {
                InitDictionary(dic, lzwMinimumCodeSize, out lzwCodeSize, out clearCode, out finishCode);
                dicInitFlag = false;
            }

            int key = bitData.GetNumeral(bitDataIndex, lzwCodeSize);

            entry.Clear();

            if (key == clearCode)
            {
                // Clear (Initialize dictionary)
                dicInitFlag = true;
                bitDataIndex += lzwCodeSize;
                prevEntry = null;
                continue;
            }
            else if (key == finishCode)
            {
                // Exit
                platform.PrintLog("gif decode error: early stop code. bitDataIndex:" + bitDataIndex + " lzwCodeSize:" + lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                break;
            }
            else if (dic.TryGetValue(key, out StringBuilder value))
            {
                // Output from dictionary
                entry.Clear();
                entry.Append(value);
            }
            else if (key >= dic.Count)
            {
                if (prevEntry != null)
                {
                    // Output from estimation
                    entry.Clear();
                    entry.Append(prevEntry);
                    entry.Append(prevEntry[0]);
                }
                else
                {
                    platform.PrintLog("weird gif compression... bitDataIndex:" + bitDataIndex + " lzwCodeSize:" + lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                    bitDataIndex += lzwCodeSize;
                    continue;
                }
            }
            else
            {
                platform.PrintLog("weird gif compression... bitDataIndex:" + bitDataIndex + " lzwCodeSize:" + lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                bitDataIndex += lzwCodeSize;
                continue;
            }

            // Output
            // Take out 8 bits from the string.
            byte[] temp = Encoding.Unicode.GetBytes(entry.ToString());
            for (int i = 0; i < temp.Length; i++)
            {
                if (i % 2 == 0)
                {
                    output[outputAddIndex] = temp[i];
                    outputAddIndex++;
                }
            }

            if (outputAddIndex >= needDataSize)
            {
                // Exit
                break;
            }

            if (prevEntry != null)
            {
                // Add to dictionary
                dic.Add(dic.Count, new StringBuilder(prevEntry + entry[0]));
            }

            prevEntry = entry.ToString();

            bitDataIndex += lzwCodeSize;

            if (lzwCodeSize > 2 && lzwCodeSize < 12 && dic.Count >= System.Math.Pow(2, lzwCodeSize))
            {
                lzwCodeSize++;
            }
            else if (lzwCodeSize == 12 && dic.Count >= 4096)
            {
                int nextKey = bitData.GetNumeral(bitDataIndex, lzwCodeSize);
                if (nextKey != clearCode)
                {
                    dicInitFlag = true;
                }
            }
        }

        return output;
    }

    public static byte[] GetDecodedData(Gif gif)
    {
        LocalImageDescriptor? imgBlock = null;
        foreach (var block in gif.Blocks)
        {
            if (block.BlockType == BlockType.LocalImageDescriptor)
            {
                imgBlock = block.Body as LocalImageDescriptor;
                break;
            }
        }

        // Combine LZW compressed data
        var lzwDataLength = 0;
        foreach (var entry in imgBlock.ImageData.Subblocks.Entries)
        {
            lzwDataLength += entry.LenBytes;
        }
        var lzwData = new byte[lzwDataLength];
        var lzwIndex = 0;
        for (int i = 0; i < imgBlock.ImageData.Subblocks.Entries.Count; i++)
        {
            Array.Copy(imgBlock.ImageData.Subblocks.Entries[i].Bytes, 0, lzwData, lzwIndex, imgBlock.ImageData.Subblocks.Entries[i].LenBytes);
            lzwIndex += imgBlock.ImageData.Subblocks.Entries[i].LenBytes;
        }

        // LZW decode
        int needDataSize = imgBlock.Height * imgBlock.Width;
        byte[] decodedData = DecodeGifLZW(lzwData, imgBlock.ImageData.LzwMinCodeSize, needDataSize);

        // Sort interlace GIF
        if (imgBlock.HasInterlace)
        {
            decodedData = SortInterlaceGifData(decodedData, imgBlock.Width);
        }
        return decodedData;
    }
}