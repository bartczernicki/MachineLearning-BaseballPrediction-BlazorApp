using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace BaseballAIWorkbench.Common.Agents
{
    public class ChatAgent
    {
        public ChatCompletionAgent ChatCompletionAgent { get; set; }
        public string AgentPrompt { get; set; }
    }
}
