using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaseballMachineLearningWorkbench
{
    public static class Util
    {
        public static string RemoveWhiteSpace(string stringWithPotentialWhiteSpace)
        {
            var whiteSpeaceRemoved = String.Concat(stringWithPotentialWhiteSpace.Where(c => !Char.IsWhiteSpace(c)));

            return whiteSpeaceRemoved;
        }
    }
}
