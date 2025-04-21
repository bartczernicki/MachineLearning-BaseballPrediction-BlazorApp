namespace BaseballAIWorkbench.Common.Agents
{
    public class Agent
    {
        public string? Name { get; set; }
        public required string AgentType { get; set; }
        public bool IsSelected { get; set; } = false;
        public string? Description { get; set; }
        public string? Instructions { get; set; }
    }
}
