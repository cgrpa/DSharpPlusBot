using System.Threading;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Services;

public sealed class ImageGenerationContextAccessor
{
    private readonly AsyncLocal<ImageGenerationExecutionContext?> _currentContext = new();
    private readonly AsyncLocal<ImageGenerationResult?> _lastResult = new();

    public ImageGenerationExecutionContext? Current => _currentContext.Value;

    public IDisposable Push(ImageGenerationExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = _currentContext.Value;
        _currentContext.Value = context;
        return new Scope(() => _currentContext.Value = previous);
    }

    public void StoreLastResult(ImageGenerationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _lastResult.Value = result;
    }

    public ImageGenerationResult? ConsumeLastResult()
    {
        var result = _lastResult.Value;
        _lastResult.Value = null;
        return result;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public Scope(Action dispose)
        {
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}
