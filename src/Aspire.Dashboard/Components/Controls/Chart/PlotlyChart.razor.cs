// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Web;
using Aspire.Dashboard.Components.Controls.Chart;
using Aspire.Dashboard.Extensions;
using Aspire.Dashboard.Model;
using Aspire.Dashboard.Model.Otlp;
using Aspire.Dashboard.Otlp.Model;
using Aspire.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aspire.Dashboard.Components;

public partial class PlotlyChart : ChartBase, IDisposable
{
    [Inject]
    public required IJSRuntime JS { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    private DotNetObjectReference<ChartInterop>? _chartInteropReference;

    private string FormatTooltip(string title, double yValue, DateTimeOffset xValue)
    {
        var formattedValue = FormatHelpers.FormatNumberWithOptionalDecimalPlaces(yValue, maxDecimalPlaces: 3, CultureInfo.CurrentCulture);
        if (InstrumentViewModel?.Instrument is { } instrument)
        {
            formattedValue += " " + InstrumentUnitResolver.ResolveDisplayedUnit(instrument, titleCase: false, pluralize: yValue != 1);
        }
        return $"<b>{HttpUtility.HtmlEncode(title)}</b><br />Value: {formattedValue}<br />Time: {FormatHelpers.FormatTime(TimeProvider, TimeProvider.ToLocal(xValue))}";
    }

    protected override async Task OnChartUpdated(List<ChartTrace> traces, List<DateTimeOffset> xValues, List<Exemplar> exemplars, bool tickUpdate, DateTimeOffset inProgressDataTime)
    {
        var traceDtos = traces.Select(t => new PlotlyTrace
        {
            Name = t.Name,
            Y = t.DiffValues,
            X = xValues,
            Tooltips = t.Tooltips,
            TraceData = new List<object?>()
        }).ToArray();

        var exemplarTraceDto = new PlotlyTrace
        {
            Name = "Exemplars",
            Y = new List<double?>(),
            X = new List<DateTimeOffset>(),
            Tooltips = new List<string?>(),
            TraceData = new List<object?>()
        };

        foreach (var exemplar in exemplars)
        {
            if (exemplar.TraceId == null || exemplar.SpanId == null)
            {
                continue;
            }

            var title = exemplar.Span != null
                ? SpanWaterfallViewModel.GetTitle(exemplar.Span, Applications)
                : $"Trace: {OtlpHelpers.ToShortenedId(exemplar.TraceId)}";
            var tooltip = FormatTooltip(title, exemplar.Value, exemplar.Start);

            exemplarTraceDto.X.Add(exemplar.Start);
            exemplarTraceDto.Y.Add(exemplar.Value);
            exemplarTraceDto.Tooltips.Add(tooltip);
            exemplarTraceDto.TraceData.Add(new { TraceId = exemplar.TraceId, SpanId = exemplar.SpanId });
        }

        if (!tickUpdate)
        {
            // The chart mostly shows numbers but some localization is needed for displaying time ticks.
            var is24Hour = DateTimeFormatInfo.CurrentInfo.LongTimePattern.StartsWith("H", StringComparison.Ordinal);
            // Plotly uses d3-time-format https://d3js.org/d3-time-format
            var time = is24Hour ? "%H:%M:%S" : "%-I:%M:%S %p";
            var userLocale = new PlotlyUserLocale
            {
                Periods = [DateTimeFormatInfo.CurrentInfo.AMDesignator, DateTimeFormatInfo.CurrentInfo.PMDesignator],
                Time = time
            };

            _chartInteropReference?.Dispose();
            _chartInteropReference = DotNetObjectReference.Create(new ChartInterop(this));

            await JS.InvokeVoidAsync("initializeChart",
                "plotly-chart-container",
                traceDtos,
                exemplarTraceDto,
                TimeProvider.ToLocal(inProgressDataTime),
                TimeProvider.ToLocal(inProgressDataTime - Duration).ToLocalTime(),
                userLocale,
                _chartInteropReference).ConfigureAwait(false);
        }
        else
        {
            await JS.InvokeVoidAsync("updateChart",
                "plotly-chart-container",
                traceDtos,
                exemplarTraceDto,
                TimeProvider.ToLocal(inProgressDataTime),
                TimeProvider.ToLocal(inProgressDataTime - Duration)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_chartInteropReference != null)
        {
            _chartInteropReference.Value.Dispose();
            _chartInteropReference.Dispose();
        }
    }

    /// <summary>
    /// Handle user clicking on a trace point in the browser.
    /// </summary>
    private sealed class ChartInterop : IDisposable
    {
        private readonly PlotlyChart _plotlyChart;
        private readonly CancellationTokenSource _cts;

        public ChartInterop(PlotlyChart plotlyChart)
        {
            _plotlyChart = plotlyChart;
            _cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _cts.Cancel();
        }

        [JSInvokable]
        public async Task ViewSpan(string traceId, string spanId)
        {
            var available = await MetricsHelpers.WaitForSpanToBeAvailableAsync(
                traceId,
                spanId,
                _plotlyChart.GetSpan,
                _plotlyChart.DialogService,
                _plotlyChart.InvokeAsync,
                _cts.Token).ConfigureAwait(false);

            if (available)
            {
                await _plotlyChart.InvokeAsync(() =>
                {
                    _plotlyChart.NavigationManager.NavigateTo(DashboardUrls.TraceDetailUrl(traceId, spanId));
                });
            }
        }
    }
}
