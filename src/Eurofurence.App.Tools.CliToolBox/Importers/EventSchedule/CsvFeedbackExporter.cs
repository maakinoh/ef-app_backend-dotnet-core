﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Eurofurence.App.Tools.CliToolBox.Importers.EventSchedule
{
    public class CsvFeedbackExporter
    {
        public class Record
        {
            public Guid EventId { get; set; }
            public string SourceEventId { get; set; }
            public string SourceEventTitle { get; set; }
            public string SourceEventSubTitle { get; set; }
            public string Slug { get; set; }
            public DateTime EventStartDateTimeUtc { get; set; }
            public DateTime EventEndDateTimeUtc { get; internal set; }
            public DateTime FeedbackReceivedDateTimeUtc { get; set; }
            public string FeedbackRelativeTime => CsvFeedbackExporter.ToRelativeDate(FeedbackReceivedDateTimeUtc, EventStartDateTimeUtc);
            public int Rating { get; set; }
            public string Message { get; set; }
        }


        static readonly SortedList<double, Func<TimeSpan, string>> offsets =
           new SortedList<double, Func<TimeSpan, string>>
        {
            { 0.75, _ => "less than a minute"},
            { 1.5, _ => "about a minute"},
            { 45, x => $"{x.TotalMinutes:F0} minutes"},
            { 90, x => "about an hour"},
            { 1440, x => $"about {x.TotalHours:F0} hours"},
            { 2880, x => "a day"},
            { 43200, x => $"{x.TotalDays:F0} days"},
            { 86400, x => "about a month"},
            { 525600, x => $"{x.TotalDays / 30:F0} months"},
            { 1051200, x => "about a year"},
            { double.MaxValue, x => $"{x.TotalDays / 365:F0} years"}
        };

        public static string ToRelativeDate(DateTime input, DateTime anchor)
        {
            TimeSpan x = anchor - input;
            string Suffix = x.TotalMinutes > 0 ? " before end" : " after end";
            x = new TimeSpan(Math.Abs(x.Ticks));
            return offsets.First(n => x.TotalMinutes < n.Key).Value(x) + Suffix;
        }

        public void Write(IEnumerable<Record> records, TextWriter output)
        {
            var writer = new CsvHelper.CsvWriter(output, true);
            writer.WriteRecords(records);
        }
    }
}
