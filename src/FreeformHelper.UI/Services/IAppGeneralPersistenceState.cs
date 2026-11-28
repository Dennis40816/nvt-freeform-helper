// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Threading;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.Services;

/// <summary>Gives the coordinator access to the existing workspace-lifetime persistence holder.</summary>
internal interface IAppGeneralPersistenceState
{
    DispatcherTimer? PersistTimer { get; set; }

    bool DeferAfterProjectLoad { get; set; }

    bool SuppressPersistence { get; set; }

    bool HasDeferredChanges { get; set; }

    UiViewSnapshot ViewSnapshot { get; set; }

    UiImportSnapshot ImportSnapshot { get; set; }
}
