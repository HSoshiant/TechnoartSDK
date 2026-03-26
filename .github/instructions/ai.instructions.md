---
applyTo: "AI/**"
---

# TechnoartSDK – AI Namespace Conventions

## Contents

| File | Class | Purpose |
|---|---|---|
| `WriterReviewerAIAgents.cs` | `WriterReviewerAIAgents` | Orchestrates a writer+reviewer SK agent pair to produce and iteratively refine AI-generated content |
| `AIAgents/AIChatCompletion.cs` | `AIChatCompletion` | Thin wrapper for single-turn chat completions |
| `AIAgents/AIImageGenerator.cs` | `AIImageGenerator` | Generates images via Azure AI Imagen |
| `AIAgents/AIVoiceAnalyzer.cs` | `AIVoiceAnalyzer` | Voice/audio analysis utilities |

## WriterReviewerAIAgents Pattern

Every AI generation step uses this pattern:

```csharp
var wrAgents = new WriterReviewerAIAgents(kernel, logger);
var result = await wrAgents.GenerateWriterReviewAsync<T>(
    operationName,
    writerInstructions: writerInst,     writerServiceName: AIServicesExtensions.OpenAIService,
    reviewerInstructions: reviewerInst, reviewerServiceName: AIServicesExtensions.GoogleAIService,
    reviewerPrompt: "",
    chatHistory: chatMessages,
    maxRound: 1);   // 0 = no review round
```

- **Writer** uses `AIServicesExtensions.OpenAIService` — drives the final output.
- **Reviewer** uses `AIServicesExtensions.GoogleAIService` — provides critique fed back for the next round.
- `maxRound: 0` skips the reviewer entirely (writer-only generation).
- Pass `responseSchema` (`JsonElement` from `JsonUtilities.GetJsonSchema<TModel, TBase>(...)`) when structured JSON output is required.
- Use `AIServicesExtensions.OpenAIServiceMini` for lightweight tasks (title generation, short summaries).

## Rules

- Keep AI agent classes in this namespace — do not add application-domain logic here.
- Constructor inject `Kernel` and `ILogger` — no other dependencies.
- SKEXP pragma suppressions are expected; always add a `//` comment explaining which experimental feature is being used.
- No nested classes — every support type gets its own file.
