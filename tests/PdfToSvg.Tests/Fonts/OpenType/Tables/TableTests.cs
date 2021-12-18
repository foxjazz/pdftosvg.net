﻿// Copyright (c) PdfToSvg.NET contributors.
// https://github.com/dmester/pdftosvg.net
// Licensed under the MIT License.

using Newtonsoft.Json;
using NUnit.Framework;
using PdfToSvg.Fonts.OpenType;
using PdfToSvg.Fonts.OpenType.Enums;
using PdfToSvg.Fonts.OpenType.Tables;
using PdfToSvg.Fonts.OpenType.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PdfToSvg.Tests.Fonts.OpenType.Tables
{
    internal class TableTests
    {
        private static void FillData(object table)
        {
            var random = new Random(0);

            foreach (var field in table.GetType().GetFields())
            {
                if (field.FieldType == typeof(ushort))
                {
                    field.SetValue(table, (ushort)random.Next(short.MaxValue, ushort.MaxValue));
                }
                else if (field.FieldType == typeof(short))
                {
                    field.SetValue(table, (short)random.Next(short.MinValue, -1));
                }
                else if (field.FieldType == typeof(uint))
                {
                    field.SetValue(table, (uint)random.Next(int.MinValue, -1));
                }
                else if (field.FieldType == typeof(int))
                {
                    field.SetValue(table, random.Next(int.MinValue, -1));
                }
                else if (field.FieldType.IsEnum)
                {
                    var values = Enum.GetValues(field.FieldType);
                    var value = values.GetValue(random.Next(0, values.Length));
                    field.SetValue(table, value);
                }
            }
        }

        private static T RoundTrip<T>(T table, Func<OpenTypeReader, IBaseTable> readCallback)
        {
            var writer = new OpenTypeWriter();
            ((IBaseTable)table).Write(writer);

            var buffer = writer.ToArray();
            var reader = new OpenTypeReader(buffer, 0, buffer.Length);

            return (T)readCallback(reader);
        }

        [Test]
        public void TestHeadTable()
        {
            var sourceTable = new HeadTable();

            FillData(sourceTable);
            sourceTable.Created = new DateTime(2014, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            sourceTable.Modified = new DateTime(2014, 5, 4, 3, 2, 1, DateTimeKind.Utc);

            var resultTable = RoundTrip(sourceTable, HeadTable.Read);

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestHheaTable()
        {
            var sourceTable = new HheaTable();

            FillData(sourceTable);

            var resultTable = RoundTrip(sourceTable, HheaTable.Read);

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestHmtxTable()
        {
            var sourceTable = new HmtxTable();

            sourceTable.HorMetrics = new[]
            {
                new LongHorMetricRecord { AdvanceWidth = ushort.MinValue, LeftSideBearing = short.MinValue },
                new LongHorMetricRecord { AdvanceWidth = ushort.MaxValue, LeftSideBearing = short.MaxValue },
                new LongHorMetricRecord { AdvanceWidth = 5, LeftSideBearing = 6 },
            };

            sourceTable.LeftSideBearings = new short[] { short.MaxValue, short.MinValue, 9 };

            var context = new OpenTypeReaderContext("hmtx", new IBaseTable[]
            {
                new HheaTable { NumberOfHMetrics = 3 },
            });
            var resultTable = RoundTrip(sourceTable, reader => HmtxTable.Read(reader, context));

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestMaxpTable()
        {
            var sourceTable = new MaxpTableV05();

            sourceTable.NumGlyphs = ushort.MaxValue;

            var resultTable = RoundTrip(sourceTable, MaxpTableV05.Read);

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestNameV0Table()
        {
            var sourceTable = new NameTable();

            FillData(sourceTable);

            sourceTable.Version = 0;

            // Should not be serialized
            sourceTable.LangTagRecords = new[]
            {
                new LangTagRecord
                {
                    Content = new byte[] { 1, 2, 3 },
                },
            };

            sourceTable.NameRecords = new[]
            {
                new NameRecord
                {
                    NameID = OpenTypeNameID.FontFamily,
                    EncodingID = 1,
                    LanguageID = 1234,
                    PlatformID = OpenTypePlatformID.Macintosh,
                    Content = new byte[] { 1, 2, 3 },
                },
                new NameRecord
                {
                    NameID = OpenTypeNameID.FontSubfamily,
                    EncodingID = 0,
                    LanguageID = 0,
                    PlatformID = OpenTypePlatformID.Windows,
                    Content = new byte[0],
                },
            };

            var resultTable = RoundTrip(sourceTable, NameTable.Read);

            sourceTable.LangTagRecords = new LangTagRecord[0];

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestNameV1Table()
        {
            var sourceTable = new NameTable();

            FillData(sourceTable);

            sourceTable.Version = 1;

            sourceTable.LangTagRecords = new[]
            {
                new LangTagRecord
                {
                    Content = new byte[] { 1, 2, 3 },
                },
                new LangTagRecord
                {
                    Content = new byte[] { 4, 5, 6 },
                },
                new LangTagRecord
                {
                    Content = new byte[0],
                },
            };

            sourceTable.NameRecords = new[]
            {
                new NameRecord
                {
                    NameID = OpenTypeNameID.FontFamily,
                    EncodingID = 1,
                    LanguageID = 1234,
                    PlatformID = OpenTypePlatformID.Macintosh,
                    Content = new byte[] { 1, 2, 3 },
                },
                new NameRecord
                {
                    NameID = OpenTypeNameID.FontSubfamily,
                    EncodingID = 0,
                    LanguageID = 0,
                    PlatformID = OpenTypePlatformID.Windows,
                    Content = new byte[0],
                },
            };

            var resultTable = RoundTrip(sourceTable, NameTable.Read);

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestPostTable()
        {
            var sourceTable = new PostTableV3();

            FillData(sourceTable);

            var resultTable = RoundTrip(sourceTable, PostTableV3.Read);

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestCMapTable()
        {
            var random = new Random(0);

            var sourceTable = new CMapTable();

            var glyphIdArray = new byte[256];
            random.NextBytes(glyphIdArray);

            sourceTable.EncodingRecords = new[]
            {
                // Format 0
                new CMapEncodingRecord
                {
                    EncodingID = 1,
                    PlatformID = OpenTypePlatformID.Windows,
                    Content = new CMapFormat0
                    {
                        Language = 0,
                        GlyphIdArray = glyphIdArray,
                    },
                },

                // Format 4
                new CMapEncodingRecord
                {
                    EncodingID = 10,
                    PlatformID = OpenTypePlatformID.Macintosh,
                    Content = new CMapFormat4
                    {
                        Language = 123,
                        StartCode = new ushort[] { 1, 2, 3, 4 },
                        EndCode = new ushort[] { 5, 6, 7, 8 },
                        IdDelta= new short[] { 9, -10, 11, -12 },
                        IdRangeOffsets = new ushort[] { 13, 14, 15, 16 },
                        GlyphIdArray = new ushort[] { 123, 124, 125, 126, 127, 128, 129, 130, 131 },
                    },
                },

                // Format 12
                new CMapEncodingRecord
                {
                    EncodingID = 100,
                    PlatformID = OpenTypePlatformID.Unicode,
                    Content = new CMapFormat12
                    {
                        Language = 0,
                        Groups = new[]
                        {
                            new CMapFormat12Group
                            {
                                StartCharCode = 1234,
                                EndCharCode = 2400,
                                StartGlyphID = 1,
                            },
                            new CMapFormat12Group
                            {
                                StartCharCode = 3214,
                                EndCharCode = 5214,
                                StartGlyphID = 10,
                            },
                        },
                    },
                },
            };

            var resultTable = RoundTrip(sourceTable, CMapTable.Read);

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [Test]
        public void TestOs2Table()
        {
            var sourceTable = new OS2Table();

            FillData(sourceTable);
            sourceTable.Version = 5;
            sourceTable.Panose = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            sourceTable.AchVendID = "abcd";

            var resultTable = RoundTrip(sourceTable, OS2Table.Read);

            Assert.AreEqual(
                JsonConvert.SerializeObject(sourceTable, Formatting.Indented),
                JsonConvert.SerializeObject(resultTable, Formatting.Indented));
        }

        [TestCase(0b0000000100000100, UsagePermission.Printable, true, false)]
        [TestCase(0b0000001000001000, UsagePermission.Editable, false, true)]
        public void TestOs2FsType(int expectedValue, UsagePermission usagePermission, bool noSubsetting, bool bitmapEmbeddingOnly)
        {
            var table = new OS2Table();

            table.UsagePermissions = usagePermission;
            table.NoSubsetting = noSubsetting;
            table.BitmapEmbeddingOnly = bitmapEmbeddingOnly;

            Assert.AreEqual((ushort)expectedValue, table.FsType);
        }
    }
}
