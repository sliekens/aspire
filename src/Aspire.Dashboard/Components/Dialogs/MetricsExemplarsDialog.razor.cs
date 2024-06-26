// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Components.Controls.Chart;
using Aspire.Dashboard.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Aspire.Dashboard.Components.Dialogs;

public partial class MetricsExemplarsDialog : IDialogContentComponent<MetricExemplarsDialogViewModel>
{
    [CascadingParameter]
    public FluentDialog Dialog { get; set; } = default!;

    [Parameter]
    public MetricExemplarsDialogViewModel Content { get; set; } = default!;

    [Inject]
    public required BrowserTimeProvider TimeProvider { get; init; }

    public IQueryable<Exemplar> MetricView => Content.Exemplars.AsQueryable();

    protected override void OnInitialized()
    {
    }

    protected override void OnParametersSet()
    {
    }
}
