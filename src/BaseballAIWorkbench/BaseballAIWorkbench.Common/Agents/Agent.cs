using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace BaseballAIWorkbench.Common.Agents
{
    public class Agent
    {
        public string? Name { get; set; }
        public required string AgentType { get; set; }
        public bool IsSelected { get; set; } = false;
        public string? Description { get; set; }
        public string? Instructions { get; set; }
        public string? AgentPrompt { get; set; }

        public ChatCompletionAgent GetChatCompletionAgent(Kernel semanticKernel)
        {
            return new ChatCompletionAgent
            {
                Kernel = semanticKernel,
                Name = this.AgentType,
                Description = this.Description,
                Instructions = this.Instructions
            };
        }
    }
}
