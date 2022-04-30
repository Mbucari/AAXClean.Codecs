using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
    public class SilenceDetectCallback
    {
        public double SilenceThreshold { get; internal init; }
        public TimeSpan MinimumDuration { get; internal init; }
        public SilenceEntry Silence { get; internal init; }
    }
}
