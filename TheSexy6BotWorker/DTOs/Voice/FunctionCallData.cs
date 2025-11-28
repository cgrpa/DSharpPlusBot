using System.ComponentModel.DataAnnotations;

namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// Details of an AI function call request to a Semantic Kernel plugin.
/// </summary>
public class FunctionCallData
{
    /// <summary>
    /// Unique call identifier from OpenAI (format: call_xxxxx).
    /// </summary>
    [Required]
    public required string CallId { get; init; }

    /// <summary>
    /// Name of the Semantic Kernel function to invoke.
    /// Must match a registered plugin function.
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// Function parameters as key-value pairs.
    /// Values are deserialized from JSON and must match function signature.
    /// </summary>
    [Required]
    public required Dictionary<string, object> Arguments { get; init; }

    /// <summary>
    /// Current execution status of the function call.
    /// </summary>
    [Required]
    public FunctionCallStatus Status { get; set; } = FunctionCallStatus.Pending;

    /// <summary>
    /// Error message if execution failed (only set when Status = Failed).
    /// </summary>
    public string? Error { get; set; }
}
