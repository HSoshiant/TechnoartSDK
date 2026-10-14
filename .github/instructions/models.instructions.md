---
applyTo: "Models/**"
---

# TechnoartSDK – Models Namespace Conventions

## Contents

| File | Class/Type | Purpose |
|---|---|---|
| `ImageModel.cs` | `ImageModel` | Represents a generated or stored image (URL, local path, metadata) |
| `ImageGenerationMetadata.cs` | `ImageGenerationMetadata` | Requested models, reported generation identity, and separate dispatcher/image usage |
| `ImageGenerationTokenUsage.cs` | `ImageGenerationTokenUsage` | Nullable reported token counts; unknown or unobservable values are never estimated |
| `RepositoryModel.cs` | `RepositoryModel` | Base class for persisted entities |
| `SDKEnums.cs` | `SDKEnums` | Shared enums used across projects (`ImageQualityType`) |
| `IFileModel.cs` | `IFileModel` | Interface for models that reference a file on disk |

## Rules

- Models here must be **domain-neutral** — they may not reference FAME-specific types.
- Use `Newtonsoft.Json` attributes for serialization where needed.
- Enums live in `SDKEnums` as nested static-class enums unless they clearly belong to a single model class, in which case they are defined alongside that model **in a separate file**.
- No business logic in model classes — pure data containers only.
- No nested classes — every type gets its own file.
- All public properties must have `/// <summary>` XML doc comments if their purpose is not immediately obvious from the name.
- Keep `ImageModel.GenerationMetadata` optional for existing consumers and JSON payloads, and `NotMapped` so existing entity models gain no relationship or schema requirement. Preserve it when normalizing image data. Image cache-hit counts are not observable; dispatcher cache counts must not be reported as image-cache savings.

## Formatting

- **Every `if`, `else`, `for`, `foreach`, `while`, and `using` block must use curly braces `{ }`, even for single-statement bodies.**
- The body must always be on its own line — never on the same line as the condition.
- Guard-clause `return`/`throw` statements follow the same rule: brace + new line.
