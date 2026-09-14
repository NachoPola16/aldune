namespace Aldune.Core;

public sealed class FanStateMachine
{
    public bool IsExpanded { get; private set; }
    public event EventHandler? ExpansionChanged;

    private bool _collapsePending;

    public void PointerEntered()
    {
        _collapsePending = false;
        if (!IsExpanded)
        {
            IsExpanded = true;
            ExpansionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void PointerLeft()
    {
        if (IsExpanded)
        {
            _collapsePending = true;
        }
    }

    public void CollapseTimerElapsed()
    {
        if (_collapsePending)
        {
            _collapsePending = false;
            IsExpanded = false;
            ExpansionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
