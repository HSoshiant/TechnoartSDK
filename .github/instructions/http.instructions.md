---
applyTo: "HTTP/**"
---

# TechnoartSDK – HTTP Namespace Conventions

## Contents

| File | Class | Purpose |
|---|---|---|
| `StreamSafeLoggingHandler.cs` | `StreamSafeLoggingHandler` | `DelegatingHandler` that logs all requests/responses without pre-reading the body (safe for SSE and NDJSON streams) |
| `TeeStream.cs` | `TeeStream` | Pass-through `Stream` wrapper that captures bytes into a buffer as the consumer reads, then fires a callback on `Dispose` |
| `RequestBodyLoggingMiddleware.cs` | `RequestBodyLoggingMiddleware` | ASP.NET Core middleware for logging incoming request bodies |
| `ResponseBodyLoggingMiddleware.cs` | `ResponseBodyLoggingMiddleware` | ASP.NET Core middleware for logging outgoing response bodies |

## Rules

### DelegatingHandler Rules
- Every handler must extend `DelegatingHandler` directly — do not extend other handlers.
- Registered as **transient** in consuming projects (`services.AddTransient<MyHandler>()`).
- Must never call `response.Content.ReadAsStringAsync()` or any other buffering read before returning the response — this would exhaust SSE/NDJSON streams.
- Use `TeeStream` to safely capture response bodies for logging without blocking the consumer.

### TeeStream Pattern
`TeeStream` wraps the real response body stream:
```
Consumer reads TeeStream.ReadAsync()
  ├─ forwards bytes from the inner stream (live, untouched)
  └─ copies same bytes into a MemoryStream buffer

On Dispose():
  └─ buffer → UTF-8 string → onDisposed callback (e.g. logger.LogInformation)
```
This means the body is logged **after** the consumer has finished reading — not before. Works identically for regular JSON and SSE/NDJSON streaming responses.

### Adding a New Handler
1. Create `NewHandler.cs` in `HTTP/` — one file per class, no nested classes.
2. Extend `DelegatingHandler`.
3. Override `SendAsync`, call `await base.SendAsync(...)` to forward the request.
4. Register as transient in the consuming project.
5. Add a `/// <summary>` XML doc comment to the class.
6. Update this instruction file.

## Formatting

- **Every `if`, `else`, `for`, `foreach`, `while`, and `using` block must use curly braces `{ }`, even for single-statement bodies.**
- The body must always be on its own line — never on the same line as the condition.
- Guard-clause `return`/`throw` statements follow the same rule: brace + new line.
