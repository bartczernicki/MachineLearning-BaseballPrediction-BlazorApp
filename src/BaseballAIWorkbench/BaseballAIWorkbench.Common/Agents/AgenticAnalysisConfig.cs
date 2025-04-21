using BaseballAIWorkbench.Common.MachineLearning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseballAIWorkbench.Common.Agents
{
    public class AgenticAnalysisConfig
    {
        public List<string> AgentsToUse { get; set; } = new List<string>();
        public MLBBaseballBatter BaseballBatter { get; set; }
    }
}
