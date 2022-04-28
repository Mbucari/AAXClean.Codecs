using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
    public class SilenceDetectCallback
    {
        public SilenceEntry Silence { get; }
        internal SilenceDetectCallback(SilenceEntry silence)
        {
            Silence = silence;
        }
    }
}
