# TechnoartSDK

## Project Overview

TechnoartSDK is the shared infrastructure library for all Technoart projects (including FAME). It provides reusable HTTP pipeline utilities, AI agent abstractions, extensions, shared models, and utilities that must not be duplicated in consuming projects.

## Solution Structure

```
TechnoartSDK/
  AI/              ← WriterReviewerAIAgents + AIAgents/
  Extensions/      ← AIServicesExtensions, HttpResponseExtensions, EnumExtensions, StringExtensions
  HTTP/            ← HTTP handlers and middleware
  Models/          ← Shared models (ImageModel, RepositoryModel, SDKEnums)
  Utils/           ← JsonUtilities
```

## Namespace Inventory

| Namespace | Contents |
|---|---|
| `TechnoartSDK.AI` | `WriterReviewerAIAgents` |
| `TechnoartSDK.AI.AIAgents` | `AIChatCompletion`, `AIImageGenerator`, `AIVoiceAnalyzer` |
| `TechnoartSDK.HTTP` | `StreamSafeLoggingHandler`, `TeeStream`, `RequestBodyLoggingMiddleware`, `ResponseBodyLoggingMiddleware` |
| `TechnoartSDK.Extensions` | `AIServicesExtensions`, `HttpResponseExtensions`, `EnumExtensions`, `StringExtensions` |
| `TechnoartSDK.Models` | `ImageModel`, `RepositoryModel`, `SDKEnums` |
| `TechnoartSDK.Utils` | `JsonUtilities` |

## Key Conventions

### Purpose Rule
- This library contains only **reusable, cross-cutting** code.
- Never add application-specific logic, project-domain models, or FAME-specific types here.
- Classes that are needed in more than one consuming project belong here; one-off utilities stay in the consuming project.

### Naming Rules
- Use `#region` blocks: `Types`, `Constructors`, `Methods`, `Fields`, `Static Fields`, `Static Methods`.
- **Never use nested classes.** Every class — including small helper or support types — must live in its own file.
- File name must match the class name exactly.

### Commenting
- All public classes and their public methods must have `/// <summary>` XML doc comments.
- Major non-obvious code blocks (pragma suppressions, streaming patterns, algorithm steps) must have a short `//` comment explaining intent.
- Do **not** comment obvious one-liners or self-explanatory LINQ chains.
- One clear comment is better than several redundant ones — prefer quality over quantity.

### Error Handling
- Log errors with `logger.LogError` before throwing.
- Throw `ArgumentNullException` for null required parameters.
- Throw `NotSupportedException` for unrecognised enum or type values.

### HTTP Handlers
- All `DelegatingHandler` subclasses must be registered as **transient** in the consuming project's DI.
- Handlers must never buffer a response stream before returning — use `TeeStream` for safe body capture.
- See `HTTP/` folder instructions for details.

### AI Agents
- All AI generation uses the `WriterReviewerAIAgents` pattern (writer + optional reviewer).
- Writer uses OpenAI; reviewer uses Google AI.
- See `AI/` folder instructions for details.
