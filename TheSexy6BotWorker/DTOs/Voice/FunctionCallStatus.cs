namespace TheSexy6BotWorker.DTOs.Voice;

/// <summary>
/// Represents the execution status of a function call from the AI.
/// </summary>
public enum FunctionCallStatus
{
    /// <summary>
    /// Function call received, waiting to execute.
    /// </summary>
    Pending,

    /// <summary>
    /// Function is currently being executed.
    /// </summary>
    Executing,

    /// <summary>
    /// Function executed successfully and result returned.
    /// </summary>
    Completed,

    /// <summary>
    /// Function execution encountered an error.
    /// </summary>
    Failed
}
