// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Describes an exportable layer independently of selection and filtering.</summary>
public sealed record LayerListEntry(string Name, int PadCount);
