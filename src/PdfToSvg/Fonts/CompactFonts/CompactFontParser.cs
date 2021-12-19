﻿// Copyright (c) PdfToSvg.NET contributors.
// https://github.com/dmester/pdftosvg.net
// Licensed under the MIT License.

using PdfToSvg.Common;
using PdfToSvg.Encodings;
using PdfToSvg.Fonts.CharStrings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PdfToSvg.Fonts.CompactFonts
{
    internal class CompactFontParser
    {
        private readonly CompactFontReader reader;
        private readonly byte[] data;
        private readonly IDictionary<uint, string>? customCMap;

        private readonly CompactFontSet fontSet = new CompactFontSet();

        internal CompactFontParser(byte[] data, IDictionary<uint, string>? customCMap)
        {
            reader = new CompactFontReader(data);
            this.data = data;
            this.customCMap = customCMap;
        }

        private void ReadDict<T>(T dict, int position, int length)
        {
            reader.Position = position;

            var dictData = reader.ReadDict(length);
            CompactFontDictSerializer.Deserialize(dict, dictData, fontSet.Strings);
        }

        private void ReadFDSelect(IList<int> fdSelect, int nGlyphs)
        {
            var format = reader.ReadCard8();

            switch (format)
            {
                case 0:
                    for (var i = 0; i < nGlyphs - 1; i++)
                    {
                        var sid = reader.ReadSID();

                        fdSelect.Add(sid);
                    }
                    break;

                case 3:
                    var nRanges = reader.ReadCard16();

                    var first = 0;
                    var fd = 0;

                    for (var i = 0; i <= nRanges; i++)
                    {
                        var nextFirst = reader.ReadCard16();

                        if (i > 0)
                        {
                            for (var j = first; j < nextFirst; j++)
                            {
                                fdSelect.Add(fd);
                            }
                        }

                        if (i < nRanges)
                        {
                            fd = reader.ReadCard8();
                            first = nextFirst;
                        }
                    }
                    break;

                default:
                    throw new CompactFontException("Invalid FDSelect format " + format + ".");
            }
        }

        private void ReadCharset(int offsetOrId, IList<int> charset, int nGlyphs)
        {
            var predefinedCharsets = CompactFontPredefinedCharsets.Charsets;

            if (offsetOrId < 0)
            {
                offsetOrId = 0;
            }

            if (offsetOrId < predefinedCharsets.Length)
            {
                foreach (var sid in predefinedCharsets[offsetOrId].Take(nGlyphs))
                {
                    charset.Add(sid);
                }
                return;
            }

            reader.Position = offsetOrId;

            var format = reader.ReadCard8();

            // The .notdef char is not included in the charset
            charset.Add(0);

            switch (format)
            {
                case 0:
                    for (var i = 0; i < nGlyphs - 1; i++)
                    {
                        var sid = reader.ReadSID();

                        charset.Add(sid);
                    }
                    break;

                case 1:
                    while (charset.Count < nGlyphs)
                    {
                        var sid = reader.ReadSID();
                        var nLeft = reader.ReadCard8();

                        for (var i = 0; i <= nLeft; i++)
                        {
                            charset.Add(sid + i);
                        }
                    }
                    break;

                case 2:
                    while (charset.Count < nGlyphs)
                    {
                        var sid = reader.ReadSID();
                        var nLeft = reader.ReadCard16();

                        for (var i = 0; i <= nLeft; i++)
                        {
                            charset.Add(sid + i);
                        }
                    }
                    break;

                default:
                    throw new CompactFontException("Invalid CFF charset format " + format + ".");
            }
        }

        private void ReadSubrs(IList<CharStringSubRoutine> target)
        {
            var subrIndex = reader.ReadIndex();

            for (var i = 1; i < subrIndex.Length; i++)
            {
                var content = new ArraySegment<byte>(data, subrIndex[i - 1], subrIndex[i] - subrIndex[i - 1]);
                target.Add(new CharStringSubRoutine(content));
            }
        }

        private void ReadFont(CompactFont font)
        {
            // Ensure supported char string type
            if (font.TopDict.CharstringType != 2)
            {
                throw new CompactFontException("Char strings of type " + font.TopDict.CharstringType + " currently not supported.");
            }

            // Private DICT
            if (font.TopDict.Private.Length == 2)
            {
                var privateDictStart = font.TopDict.Private[1];
                ReadDict(font.PrivateDict, privateDictStart, font.TopDict.Private[0]);

                if (font.PrivateDict.Subrs != null)
                {
                    reader.Position = font.PrivateDict.Subrs.Value + privateDictStart;
                    ReadSubrs(font.Subrs);
                }
            }

            // Charstrings index
            reader.Position = font.TopDict.CharStrings;
            var charStringsIndex = reader.ReadIndex();
            var nGlyphs = charStringsIndex.Length - 1;

            // Charset
            ReadCharset(font.TopDict.Charset, font.CharSet, nGlyphs);

            // FDSelect
            if (font.TopDict.FDSelect != null)
            {
                reader.Position = font.TopDict.FDSelect.Value;
                ReadFDSelect(font.FDSelect, nGlyphs);
            }

            // FDArray
            if (font.TopDict.FDArray != null)
            {
                ReadFDArray(font);
            }

            // Glyphs
            for (var glyphIndex = 0; glyphIndex < font.CharSet.Count; glyphIndex++)
            {
                ReadGlyph(font, charStringsIndex, glyphIndex);
            }

            ReplaceSeacChars(font);
        }

        private void ReadFDArray(CompactFont font)
        {
            if (font.TopDict.FDArray == null)
            {
                return;
            }

            reader.Position = font.TopDict.FDArray.Value;

            var fdArrayIndex = reader.ReadIndex();

            for (var j = 0; j + 1 < fdArrayIndex.Length; j++)
            {
                var fdFont = new CompactSubFont();
                font.FDArray.Add(fdFont);

                reader.Position = fdArrayIndex[j];
                var fdDictData = reader.ReadDict(fdArrayIndex[j + 1] - fdArrayIndex[j]);
                CompactFontDictSerializer.Deserialize(fdFont.FontDict, fdDictData, fontSet.Strings);

                var subrsFound = false;

                if (fdFont.FontDict.Private.Length == 2)
                {
                    var privateDictStart = fdFont.FontDict.Private[1];
                    var fdPrivateDict = new CompactFontPrivateDict();

                    ReadDict(fdPrivateDict, privateDictStart, fdFont.FontDict.Private[0]);

                    if (fdPrivateDict.Subrs != null)
                    {
                        reader.Position = fdPrivateDict.Subrs.Value + privateDictStart;
                        ReadSubrs(fdFont.Subrs);
                        subrsFound = true;
                    }
                }

                if (!subrsFound)
                {
                    fdFont.Subrs = font.Subrs;
                }
            }
        }

        private void ReplaceSeacChars(CompactFont font)
        {
            for (var glyphIndex = 0; glyphIndex < font.Glyphs.Count; glyphIndex++)
            {
                var glyph = font.Glyphs[glyphIndex];
                var seac = glyph.CharString.Seac;
                if (seac != null)
                {
                    var content = glyph.CharString.Content;
                    var contentInlinedSubrs = glyph.CharString.ContentInlinedSubrs;
                    var standardEncoding = new StandardEncoding();

                    var acharValue = standardEncoding.GetString(new byte[] { (byte)seac.Achar });
                    var bcharValue = standardEncoding.GetString(new byte[] { (byte)seac.Bchar });

                    var achar = font.Glyphs.FirstOrDefault(x => x.Unicode == acharValue);
                    var bchar = font.Glyphs.FirstOrDefault(x => x.Unicode == bcharValue);

                    var mergedCharString = SeacMerger.Merge(achar.CharString, bchar.CharString, seac.Adx, seac.Ady);

                    content.Clear();
                    contentInlinedSubrs.Clear();

                    content.Add(CharStringLexeme.Operand(glyph.Width - font.PrivateDict.NominalWidthX));

                    foreach (var lexeme in mergedCharString)
                    {
                        content.Add(lexeme);
                        contentInlinedSubrs.Add(lexeme);
                    }

                    if (content.LastOrDefault().OpCode != CharStringOpCode.EndChar)
                    {
                        content.Add(CharStringLexeme.Operator(CharStringOpCode.EndChar));
                    }

                    if (contentInlinedSubrs.LastOrDefault().OpCode != CharStringOpCode.EndChar)
                    {
                        contentInlinedSubrs.Add(CharStringLexeme.Operator(CharStringOpCode.EndChar));
                    }
                }
            }
        }

        private void ReadGlyph(CompactFont font, int[] charStringsIndex, int glyphIndex)
        {
            var isCidFont = font.TopDict.FDArray != null;
            var charLocalSubrs = font.Subrs;

            if (glyphIndex < font.FDSelect.Count)
            {
                var fdIndex = font.FDSelect[glyphIndex];
                if (fdIndex < font.FDArray.Count)
                {
                    charLocalSubrs = font.FDArray[fdIndex].Subrs;
                }
            }

            var startIndex = charStringsIndex[glyphIndex];
            var endIndex = glyphIndex + 1 < charStringsIndex.Length ? charStringsIndex[glyphIndex + 1] : data.Length;
            var cidOrSid = font.CharSet[glyphIndex];

            string unicode;
            CharString charString;
            double width;

            if (isCidFont)
            {
                if (customCMap == null ||
                    customCMap.TryGetValue((uint)cidOrSid, out unicode) == false)
                {
                    unicode = Utf16Encoding.GetPrivateUseChar(cidOrSid);
                }
            }
            else
            {
                var charName = fontSet.Strings.Lookup(cidOrSid);

                AdobeGlyphList.TryMap(charName, out unicode!);

                if (unicode == null)
                {
                    unicode = "";
                }
            }

            try
            {
                var charStringData = new ArraySegment<byte>(data, startIndex, endIndex - startIndex);
                charString = Type2CharStringParser.Parse(charStringData, font.FontSet.Subrs, charLocalSubrs);
            }
            catch (Exception ex)
            {
                Log.WriteLine("Failed to parse char '" + unicode + "'. " + ex);
                charString = CharString.Empty;
            }

            if (charString.Width == null)
            {
                width = font.PrivateDict.DefaultWidthX;
            }
            else
            {
                width = font.PrivateDict.NominalWidthX + charString.Width.Value;
            }

            var glyph = new CompactFontGlyph(charString, unicode, glyphIndex, cidOrSid, width);
            font.Glyphs.Add(glyph);
        }

        private CompactFontSet Read()
        {
            var header = reader.ReadHeader();

            if (header.Major != 1)
            {
                throw new CompactFontException("Unsupported CFF version " + header.Major + "." + header.Minor + ".");
            }

            reader.Position = header.HdrSize;

            var nameIndex = reader.ReadIndex();
            var topDictIndex = reader.ReadIndex();
            var stringIndex = reader.ReadIndex();
            ReadSubrs(fontSet.Subrs);

            var names = reader.ReadStrings(nameIndex);
            fontSet.Strings = new CompactFontStringTable(reader.ReadStrings(stringIndex));

            for (var i = 0; i + 1 < topDictIndex.Length; i++)
            {
                var font = new CompactFont(fontSet);
                ReadDict(font.TopDict, topDictIndex[i], topDictIndex[i + 1] - topDictIndex[i]);

                if (i < names.Length)
                {
                    font.Name = names[i];
                }
                else if (0 < names.Length)
                {
                    font.Name = names[0];
                }

                ReadFont(font);

                fontSet.Fonts.Add(font);
            }

            return fontSet;
        }

        public static CompactFontSet Parse(byte[] data, IDictionary<uint, string>? customCMap = null)
        {
            return new CompactFontParser(data, customCMap).Read();
        }
    }
}
