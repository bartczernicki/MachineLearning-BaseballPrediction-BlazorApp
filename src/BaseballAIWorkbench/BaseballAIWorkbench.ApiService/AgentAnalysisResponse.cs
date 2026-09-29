using System.Text.Json;
using System.Text.Json.Serialization;
using BaseballAIWorkbench.Common.Agents;
using Microsoft.Extensions.AI;

namespace BaseballAIWorkbench.ApiService;

// Shared by production agents and the opt-in prompt evaluator.
public static class AgentAnalysisResponse
{
    private static readonly JsonElement Schema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "AnalysisMarkdown": { "type": "string" },
            "BallotAppearanceProbability": { "type": ["number", "null"] },
            "InductionProbability": { "type": ["number", "null"] },
            "AbstentionReason": { "type": ["string", "null"] }
          },
          "required": ["AnalysisMarkdown", "BallotAppearanceProbability", "InductionProbability", "AbstentionReason"],
          "additionalProperties": false
        }
        """);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ChatOptions CreateChatOptions() => new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema(Schema, "AgentAnalysisResult"),
        // The Responses adapter does not enable strictness from ResponseFormat alone.
        AdditionalProperties = new() { ["strict"] = true }
    };

    public static AgentAnalysisResult ParseFinalMessage(IEnumerable<ChatMessage> messages)
    {
        // AgentResponse.Text includes intermediate assistant prose from tool rounds.
        var finalMessage = messages.LastOrDefault(message => message.Role == ChatRole.Assistant);
        if (finalMessage is null || finalMessage.Contents.Any(content => content is FunctionCallContent))
        {
            throw new InvalidOperationException("The agent did not return a final structured analysis.");
        }

        return Parse(finalMessage.Text);
    }

    public static AgentAnalysisResult Parse(string json)
    {
        AgentAnalysisResult result;
        try
        {
            result = JsonSerializer.Deserialize<AgentAnalysisResult>(json, SerializerOptions)
                ?? throw new JsonException("The analysis cannot be null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The agent returned an invalid structured analysis.", ex);
        }

        Validate(result);
        return result;
    }

    public static void Validate(AgentAnalysisResult result)
    {
        if (string.IsNullOrWhiteSpace(result.AnalysisMarkdown))
        {
            throw new InvalidOperationException("The agent did not return an analysis explanation.");
        }

        ValidateProbability(result.BallotAppearanceProbability, "Ballot Appearance");
        ValidateProbability(result.InductionProbability, "Induction");
        if ((!result.BallotAppearanceProbability.HasValue || !result.InductionProbability.HasValue)
            && string.IsNullOrWhiteSpace(result.AbstentionReason))
        {
            throw new InvalidOperationException("An abstained probability requires an explanation of the insufficient evidence.");
        }
    }

    private static void ValidateProbability(double? probability, string criterion)
    {
        if (probability.HasValue && (!double.IsFinite(probability.Value) || probability.Value is < 0.0 or > 1.0))
        {
            throw new InvalidOperationException($"The {criterion} probability must be a finite number between 0 and 1.");
        }
    }
}
