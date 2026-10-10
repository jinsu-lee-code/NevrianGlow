using System;
using NevrianGlowFarm.Logic.Data;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class CsvTableTests
    {
        [Test]
        public void HeaderAndRows_AreRead()
        {
            var t = CsvTable.Parse("id,seedPrice,sellPrice\ncrop_moonroot,10,15\ncrop_mushroomflower,20,40\n");

            Assert.AreEqual(3, t.Headers.Count);
            Assert.AreEqual(2, t.RowCount);
            Assert.AreEqual("crop_mushroomflower", t.Get(1, "id"));
            Assert.AreEqual("40", t.Get(1, "sellPrice"));
        }

        [Test]
        public void Bom_CrLf_BlankLines_Spaces_AreIgnored()
        {
            var t = CsvTable.Parse("﻿ id , seedPrice \r\n\r\ncrop_moonroot , 10 \r\n\r\n");

            Assert.AreEqual(1, t.RowCount);
            Assert.AreEqual("crop_moonroot", t.Get(0, "ID"), "열 이름은 대소문자 · 공백 무시");
            Assert.AreEqual("10", t.Get(0, "seedPrice"));
        }

        [Test]
        public void QuotedFields_KeepCommasQuotesAndNewlines()
        {
            var t = CsvTable.Parse("id,displayName\ncrop_a,\"달, \"\"빛\"\" 뿌리\"\ncrop_b,\"두\n줄\"\n");

            Assert.AreEqual(2, t.RowCount);
            Assert.AreEqual("달, \"빛\" 뿌리", t.Get(0, "displayName"));
            Assert.AreEqual("두\n줄", t.Get(1, "displayName"));
        }

        [Test]
        public void ShortRow_MissingCellsAreEmpty_UnknownColumnIsEmpty()
        {
            var t = CsvTable.Parse("id,a,b\nx,1");

            Assert.AreEqual("", t.Get(0, "b"));
            Assert.AreEqual("", t.Get(0, "nope"));
            Assert.AreEqual(-1, t.ColumnIndex("nope"));
        }

        [Test]
        public void Empty_Or_UnclosedQuote_Throws()
        {
            Assert.Throws<FormatException>(() => CsvTable.Parse("\n\n"));
            Assert.Throws<FormatException>(() => CsvTable.Parse("id\n\"abc"));
        }

        [Test]
        public void Values_ParseInvariant()
        {
            Assert.IsTrue(CsvValue.TryInt("12", out int i)); Assert.AreEqual(12, i);
            Assert.IsFalse(CsvValue.TryInt("1.5", out _));
            Assert.IsTrue(CsvValue.TryFloat("0.02", out float f)); Assert.AreEqual(0.02f, f, 1e-6f);
            Assert.IsFalse(CsvValue.TryFloat("0,02", out _), "소수점은 점(.)만");
            Assert.IsTrue(CsvValue.TryBool("O", out bool o)); Assert.IsTrue(o);
            Assert.IsTrue(CsvValue.TryBool("x", out bool x)); Assert.IsFalse(x);
            Assert.IsFalse(CsvValue.TryBool("maybe", out _));
        }

        [Test]
        public void Color_HexWithOrWithoutAlpha()
        {
            Assert.IsTrue(CsvValue.TryColor("#62F2E1", out float r, out float g, out float b, out float a));
            Assert.AreEqual(0x62 / 255f, r, 1e-6f);
            Assert.AreEqual(0xF2 / 255f, g, 1e-6f);
            Assert.AreEqual(0xE1 / 255f, b, 1e-6f);
            Assert.AreEqual(1f, a);
            Assert.IsTrue(CsvValue.TryColor("FF74D880", out _, out _, out _, out a));
            Assert.AreEqual(0x80 / 255f, a, 1e-6f);
            Assert.IsFalse(CsvValue.TryColor("#12345", out _, out _, out _, out _));
        }
    }
}
