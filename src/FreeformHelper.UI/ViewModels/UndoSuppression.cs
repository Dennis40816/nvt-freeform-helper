namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Nesting-safe undo suppression for use on the UI thread only.
/// </summary>
internal sealed class UndoSuppression
{
    private int _depth;

    public bool IsActive => _depth > 0;

    public Scope Enter() => new(this);

    public sealed class Scope : IDisposable
    {
        private UndoSuppression? _owner;

        internal Scope(UndoSuppression owner)
        {
            _owner = owner;
            owner._depth++;
        }

        public void Dispose()
        {
            if (_owner is null)
            {
                return;
            }

            _owner._depth--;
            _owner = null;
        }
    }
}
