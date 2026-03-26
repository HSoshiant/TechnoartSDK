---
applyTo: "Extensions/**"
---

# TechnoartSDK – Extensions Namespace Conventions

## Contents

| File | Class | Purpose |
|---|---|---|
| `AIServicesExtensions.cs` | `AIServicesExtensions` | Registers OpenAI and Google AI keyed `IChatCompletionService` instances; defines service name constants |
| `HttpResponseExtensions.cs` | `HttpResponseExtensions` | Extension methods for reading NDJSON / streaming HTTP responses |
| `EnumExtensions.cs` | `EnumExtensions` | Enum helpers (e.g. safe parse, display names) |
| `StringExtensions.cs` | `StringExtensions` | General string utilities |

## Service Name Constants (AIServicesExtensions)

| Constant | Model | Usage |
|---|---|---|
| `OpenAIService` | `gpt-5.2` | Heavy generation, chat agent |
| `OpenAIServiceMini` | `gpt-4.1-mini` | Lightweight tasks (titles, short summaries) |
| `GoogleAIService` | `gemini-3-flash-preview` | Reviewer in writer+reviewer pairs |
| `GoogleAIServicePro` | `gemini-3-pro-preview` | Heavy Google generation tasks |

## Rules

- All extension classes must be `public static`.
- Every method must be a true extension method (`this` first parameter) or a static factory helper — no instance state.
- `AIServicesExtensions.AddSemanticKernel()` is the single entry point for wiring all AI services in a consuming project.
- Do not hardcode API keys in new code — use configuration or the existing constants in `AIServicesExtensions` until a secrets manager is wired.
- No nested classes — every support type gets its own file.
