﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AAXClean.AudioFilters
{
    public class SilenceEntry
    {
        public TimeSpan SilenceStart { get; }
        public TimeSpan SilenceEnd { get; }
        public TimeSpan SilenceDuration { get; }
        internal SilenceEntry(TimeSpan start, TimeSpan end)
        {
            SilenceStart = start;
            SilenceEnd = end;
            SilenceDuration = end - start;
        }
        public override string ToString()
        {
            return $"[Start = {SilenceStart:hh\\:mm\\:ss\\:fff}, End = {SilenceEnd:hh\\:mm\\:ss\\:fff}, Duration = {SilenceDuration.TotalSeconds:F3} s]";
        }
    }
}
