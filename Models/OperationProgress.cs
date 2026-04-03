namespace TechnoartSDK.Models;

/// <summary>
/// Describes the type of progress being reported by a long-running operation.
/// </summary>
public enum OperationProgressType
{
    /// <summary>The operation has been queued but not yet started.</summary>
    Queued,

    /// <summary>The operation has started.</summary>
    Started,

    /// <summary>A named sub-step has started (e.g. writer generating, image rendering).</summary>
    StepStarted,

    /// <summary>A named sub-step has completed and may carry content.</summary>
    StepCompleted,

    /// <summary>An informational status message.</summary>
    Info,

    /// <summary>A non-fatal warning (e.g. validation errors that trigger a retry).</summary>
    Warning,

    /// <summary>The operation completed successfully.</summary>
    Completed,

    /// <summary>The operation was cancelled by the user.</summary>
    Cancelled,

    /// <summary>The operation failed.</summary>
    Failed
}

/// <summary>
/// A generalized progress report emitted by long-running operations such as
/// writer-reviewer AI exchanges, image generation, video generation, and downloads.
/// </summary>
public class OperationProgress
{
    /// <summary>The type of this progress update.</summary>
    public OperationProgressType Type { get; init; }

    /// <summary>Human-readable name of the current step (e.g. "Writer", "Reviewer", "ImageGeneration", "VideoDownload").</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Full content produced by the step, if any (e.g. writer's draft, reviewer's critique, error message).</summary>
    public string? Content { get; init; }

    /// <summary>Current item index for batch operations (zero-based, e.g. character 2 of 5).</summary>
    public int? ItemIndex { get; init; }

    /// <summary>Total number of items in a batch operation.</summary>
    public int? TotalItems { get; init; }
}
