// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Aspire.Dashboard.Components.Controls.Chart;
using Aspire.Dashboard.Model;
using Aspire.Dashboard.Model.Otlp;
using Aspire.Dashboard.Otlp.Model;
using Aspire.Dashboard.Otlp.Storage;
using Aspire.Dashboard.Utils;
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

    [Inject]
    public required IDialogService DialogService { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    public IQueryable<Exemplar> MetricView => Content.Exemplars.AsQueryable();

    protected override void OnInitialized()
    {
    }

    protected override void OnParametersSet()
    {
    }

    public async Task OnViewDetailsAsync(Exemplar exemplar)
    {
        var available = await MetricsHelpers.WaitForSpanToBeAvailableAsync(
            traceId: exemplar.TraceId!,
            spanId: exemplar.SpanId!,
            getSpan: (traceId, spanId) => MetricsHelpers.GetSpan(TelemetryRepository, traceId, spanId),
            DialogService,
            CancellationToken.None).ConfigureAwait(false);

        if (available)
        {
            NavigationManager.NavigateTo(DashboardUrls.TraceDetailUrl(exemplar.TraceId!, spanId: exemplar.SpanId));
        }
    }

    private string GetTitle(Exemplar exemplar)
    {
        return (exemplar.Span != null)
            ? SpanWaterfallViewModel.GetTitle(exemplar.Span, Content.Applications)
            : $"Trace: {OtlpHelpers.ToShortenedId(exemplar.TraceId!)}";
    }

    private static string FormatMetricValue(double? value)
    {
        return value is null ? string.Empty : value.Value.ToString("F3", CultureInfo.CurrentCulture);
    }
}
