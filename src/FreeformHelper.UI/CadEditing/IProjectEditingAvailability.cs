// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;

namespace FreeformHelper.UI.CadEditing;

public interface IProjectEditingAvailability : INotifyPropertyChanged
{
    bool IsProjectEditingEnabled { get; }
}
