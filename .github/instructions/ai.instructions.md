---
applyTo: "AI/**"
---

# TechnoartSDK – AI Namespace Conventions

## Contents

| File | Class | Purpose |
|---|---|---|
| `WriterReviewerAIAgents.cs` | `WriterReviewerAIAgents` | Orchestrates a writer+reviewer SK agent pair to produce and iteratively refine AI-generated content |
| `AIAgents/AIChatCompletion.cs` | `AIChatCompletion` | Thin wrapper for single-turn chat completions |
| `AIAgents/AIImageGenerator.cs` | `AIImageGenerator` | JSON-selected Gemini/OpenAI images; retains explicit OpenAI and Google Imagen entry points |
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
- Chat agents inject `Kernel` and `ILogger`. The image generator uses `ILogger`, `IOptions<AIServicesConfig>` and a caller-provided typed `HttpClient`.
- SKEXP pragma suppressions are expected; always add a `//` comment explaining which experimental feature is being used.
- No nested classes — every support type gets its own file.

## Configured image generation

- `AIServices.ImageService` selects `Gemini` or `OpenAI`; `ImageModel` is the provider model ID, forwarded unchanged. Defaults are Gemini / `gemini-nano-banana-2.1`.
- Use the existing Google/OpenAI API keys. Do not put domain-specific shot, cast or review rules in the SDK.
- `CreateImageAsync` supports initial generation and named multi-reference edits with exact PNG normalization. Preserve reference names, order and data.
- Gemini uses the Interactions image endpoint with 2K output; Low/Medium/High map to minimal/medium/high thinking. XHigh also uses high thinking, not a separate native quality.
- Configured OpenAI models must support GPT Image reference edits; the adapter accepts XHigh only for the GPT Image 2.5 models (`gpt-image-2.5-sunburst`, `gpt-image-2.5-flare`) and their `-2026-09-08` snapshots. Model IDs live in shared constants (`GPTImage25SunburstModel`, `GPTImage25FlareModel`); never hard-code them. Legacy explicit model entry points remain available.
- Only those GPT Image 2.5 IDs use custom native sizes: multiples of 16, edges at most 3840, aspect ratios from 1:3 through 3:1, and 655,360 through 8,294,400 pixels. Preserve valid requested native sizes, including explicitly requested experimental sizes. For other requests, search the valid grid capped at 3,686,400 nonexperimental pixels, minimize cropping first, then prefer the closest size that avoids upscaling; if none at that ratio avoids upscaling, use the size requiring the least upscaling. Ties use ascending native width then height. Keep the caller's quality unchanged. See the [OpenAI size constraints](https://developers.openai.com/api/docs/guides/image-prompting).
- Delivered dimensions still accept 64 through 4096 per edge. For GPT Image 2.5, 1080x1920 uses native 1152x2048 (and landscape uses 2048x1152), then resizes without clipping. Other examples: 800x800 uses 816x816; 1024x1024 stays unchanged; 64x128 uses 576x1152; 4096x4096 uses 1920x1920 and upscales; 64x4096 uses 1104x3312, upscales and center-crops. Final normalization allocates only the requested destination, not an oversized scaled intermediate.
- OpenAI composition guidance must identify the selected native and delivered dimensions, omit crop instructions when their ratios match, and retain safe-area guidance for actual cropping. Configured preflight and submission must use the same size selection and final composed prompt. Leave Gemini/Imagen composition wording and legacy GPT Image 1/1.5 and DALL-E native size menus unchanged.
- Validate dimensions, reference data, provider/model compatibility and request limits before submission. Gemini accepts at most 14 references and a 20 MB inline request; OpenAI accepts at most 16 references. The edit candidate consumes a reference slot.
- GPT Image prompts allow 32,000 Unicode characters, including the SDK's final composition guidance. Validate the exact composed prompt during configured preflight and again at both OpenAI transport entry points, including legacy callers. Never truncate prompts or apply this OpenAI limit to Gemini.
- Never silently drop references, substitute models or retry paid requests. OpenAI SDK retries are disabled; the consuming application's typed client must also remove any inherited retry/resilience handlers for both providers.
- Do not log request bodies or API keys. Keep cancellation and provider failures explicit; reject incomplete, substituted or unusable Gemini outputs.

## Opt-in Responses image transport

- `AIServices.OpenAIImageTransport` defaults to `Images`. Only configured `CreateImageAsync` honors `Responses`; explicit legacy OpenAI methods keep their existing transport.
- Responses requires an explicit `OpenAIImageResponsesModel`, independently of chat settings. The verified dispatcher IDs are `gpt-5.4-mini` and `gpt-5.4-mini-2026-03-17`; the verified image models are the existing Sunburst alias and snapshot. Do not infer tool support from a `gpt-5` prefix or silently choose a more expensive model.
- Send one stateless request with one forced image tool, `max_tool_calls: 1`, `parallel_tool_calls: false`, no reasoning, no streaming/partial images, `store: false`, and truncation disabled. Keep the selected native dimensions, quality and PNG output unchanged. Reuse the injected typed client without retries or body logging.
- Send original named image data exactly once, in caller order, before the varying composed prompt. Never resize, recompress, sort or relabel references for caching. The first attachment remains first even when that limits prefix reuse. Do not create file-upload caches or depend on response history.
- Validate configuration, the exact composed prompt and the 512 MB request limit before submission. Exact PNG decoding/normalization uses the existing Windows-only System.Drawing pipeline; reject unsupported hosts before paid work. Reject unsuccessful/incomplete/refused responses, extra or missing image calls, contradictory returned settings and invalid or wrong-sized output images without replay.
- `ImageModel.GenerationMetadata` preserves reported identities and separate dispatcher/image usage through normalization. `ImageGenerationException.GenerationMetadata` retains available facts on submitted-request failures. Cancellation remains cancellation and does not imply that paid work did not occur.
- Missing usage stays null. Root Responses usage belongs to the dispatcher; never treat it as image-tool usage. The public image-call schema does not guarantee an actual image model or image usage, and echoed tool settings are not actual-model evidence. Image cache-hit counts are unobservable and must remain null.
- Cached image-input pricing applies to Responses, not direct Images edits. Dispatcher usage is additional; cache eligibility does not promise hits, retention or lower total cost. See the official [image guide](https://developers.openai.com/api/docs/guides/image-generation), [Responses schema](https://developers.openai.com/api/reference/resources/responses/methods/create), and [dispatcher model](https://developers.openai.com/api/docs/models/gpt-5.4-mini).

## Formatting

- **Every `if`, `else`, `for`, `foreach`, `while`, and `using` block must use curly braces `{ }`, even for single-statement bodies.**
- The body must always be on its own line — never on the same line as the condition.
- Guard-clause `return`/`throw` statements follow the same rule: brace + new line.
