// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public partial class CoordinatePlannerCornerCircleSectionView : UserControl
{
    public CoordinatePlannerCornerCircleSectionView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is CoordinatePlannerWorkspaceViewModel viewModel)
            {
                viewModel.CornerCircles.RequestExportAsync = SaveImageAsync;
            }
        };
    }

    private async Task<bool> SaveImageAsync(CornerCircleImage image)
    {
        var outcome = await CornerCircleImageExportHost.SaveAsync(TopLevel.GetTopLevel(this), image);
        return outcome == TextExportOutcome.Saved;
    }
}
