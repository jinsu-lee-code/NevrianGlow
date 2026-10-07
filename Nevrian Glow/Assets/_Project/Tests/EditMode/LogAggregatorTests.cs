using System;
using NevrianGlowFarm.Logic.Logging;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class LogAggregatorTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 7, 14, 3, 21);
        private static readonly LogContext Ctx = new LogContext(2, "농사", "Farm", "0.1.0");

        [Test]
        public void SameMessageAndStack_IsGroupedAndCounted()
        {
            var agg = new LogAggregator(10);

            var first = agg.Record(LogSeverity.Exception, "boom", "at A()", T0, Ctx);
            var second = agg.Record(LogSeverity.Exception, "boom", "at A()", T0.AddSeconds(5), Ctx);

            Assert.AreEqual(LogRecordResult.New, first);
            Assert.AreEqual(LogRecordResult.Repeated, second);
            Assert.AreEqual(1, agg.Entries.Count);
            Assert.AreEqual(2, agg.Entries[0].Count);
            Assert.AreEqual(T0, agg.Entries[0].FirstTime);
            Assert.AreEqual(T0.AddSeconds(5), agg.Entries[0].LastTime);
        }

        [Test]
        public void SameMessage_DifferentStack_IsSeparate()
        {
            var agg = new LogAggregator(10);

            agg.Record(LogSeverity.Error, "boom", "at A()", T0, Ctx);
            agg.Record(LogSeverity.Error, "boom", "at B()", T0, Ctx);

            Assert.AreEqual(2, agg.Entries.Count);
        }

        [Test]
        public void OverMaxEntries_IsDroppedAndCounted()
        {
            var agg = new LogAggregator(1);

            agg.Record(LogSeverity.Warning, "a", "", T0, Ctx);
            var result = agg.Record(LogSeverity.Warning, "b", "", T0, Ctx);
            var repeat = agg.Record(LogSeverity.Warning, "a", "", T0, Ctx);

            Assert.AreEqual(LogRecordResult.Dropped, result);
            Assert.AreEqual(LogRecordResult.Repeated, repeat);
            Assert.AreEqual(1, agg.Entries.Count);
            Assert.AreEqual(1, agg.DroppedCount);
        }

        [Test]
        public void FormatLine_HasFieldsInOrder_OnOneLine()
        {
            var agg = new LogAggregator(10);
            agg.Record(LogSeverity.Exception, "boom\nsecond line", "at A()\nat B()\n", T0, Ctx);
            agg.Record(LogSeverity.Exception, "boom\nsecond line", "at A()\nat B()\n", T0.AddSeconds(9), Ctx);

            string line = LogLineFormatter.FormatLine(agg.Entries[0]);

            Assert.AreEqual(
                "2026-10-07 14:03:21 | Exception | Day 2 | 농사 | Farm | v0.1.0 | x2 (last 14:03:30) | boom ↵ second line | at A() ↵ at B()",
                line);
        }

        [Test]
        public void FormatLine_MissingContext_UsesDash()
        {
            var agg = new LogAggregator(10);
            agg.Record(LogSeverity.Warning, "w", null, T0, new LogContext(0, null, "", null));

            string line = LogLineFormatter.FormatLine(agg.Entries[0]);

            Assert.AreEqual("2026-10-07 14:03:21 | Warning | Day 0 | - | - | v- | x1 | w | -", line);
        }

        [Test]
        public void FormatFile_HasHeaderOneLinePerEntryAndDroppedNote()
        {
            var agg = new LogAggregator(1);
            agg.Record(LogSeverity.Error, "a", "s", T0, Ctx);
            agg.Record(LogSeverity.Error, "b", "s", T0, Ctx);

            string[] lines = LogLineFormatter.FormatFile("헤더", agg).TrimEnd('\n').Split('\n');

            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("# 헤더", lines[0]);
            StringAssert.Contains(" | a | s", lines[1]);
            StringAssert.StartsWith("# ", lines[2]);
        }
    }
}
