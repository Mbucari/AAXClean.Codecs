using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
    public class SilenceDetectCallback
    {
        public double SilenceThreshold { get; }
        public TimeSpan MinDuration { get; }
        public SilenceEntry Silence { get; }
        internal SilenceDetectCallback(double threshold, TimeSpan minDuration, SilenceEntry silence)
        {
            SilenceThreshold = threshold;
            MinDuration = minDuration;
            Silence = silence;
        }
    }
}
