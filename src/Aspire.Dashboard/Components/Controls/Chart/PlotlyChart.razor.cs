// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Web;
using Aspire.Dashboard.Components.Controls.Chart;
using Aspire.Dashboard.Extensions;
using Aspire.Dashboard.Model;
using Aspire.Dashboard.Model.Otlp;
using Aspire.Dashboard.Otlp.Model;
using Aspire.Dashboard.Otlp.Storage;
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
    public required TelemetryRepository TelemetryRepository { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    private DotNetObjectReference<ChartInterop>? _chartInteropReference;

    // Stores a cache of the last set of spans returned as exemplars.
    // This dictionary is replaced each time the chart is updated.
    private Dictionary<SpanKey, OtlpSpan> _traceCache = new Dictionary<SpanKey, OtlpSpan>();

    private readonly record struct SpanKey(string TraceId, string SpanId);

    private string FormatTooltip(string title, double yValue, DateTimeOffset xValue)
    {
        var formattedValue = FormatHelpers.FormatNumberWithOptionalDecimalPlaces(yValue, maxDecimalPlaces: 3, CultureInfo.CurrentCulture);
        if (InstrumentViewModel?.Instrument is { } instrument)
        {
            formattedValue += " " + InstrumentUnitResolver.ResolveDisplayedUnit(instrument, titleCase: false, pluralize: yValue != 1);
        }
        return $"<b>{HttpUtility.HtmlEncode(title)}</b><br />Value: {formattedValue}<br />Time: {FormatHelpers.FormatTime(TimeProvider, TimeProvider.ToLocal(xValue))}";
    }

    protected override async Task OnChartUpdated(List<ChartTrace> traces, List<DateTimeOffset> xValues, List<ExemplarPoint> exemplarPoints, bool tickUpdate, DateTimeOffset inProgressDataTime)
    {
        var traceDtos = traces.Select(t => new PlotlyTrace
        {
            Name = t.Name,
            Y = t.DiffValues,
            X = xValues,
            Tooltips = t.Tooltips,
            TraceData = new List<object?>()
        }).ToArray();

        var currentCache = _traceCache;
        var newCache = new Dictionary<SpanKey, OtlpSpan>();

        var exemplarTraceDto = new PlotlyTrace
        {
            Name = "exemplars",
            Y = new List<double?>(),
            X = new List<DateTimeOffset>(),
            Tooltips = new List<string?>(),
            TraceData = new List<object?>()
        };

        foreach (var exemplarPoint in exemplarPoints)
        {
            if (exemplarPoint.TraceId == null || exemplarPoint.SpanId == null)
            {
                continue;
            }

            var key = new SpanKey(exemplarPoint.TraceId, exemplarPoint.SpanId);
            if (!currentCache.TryGetValue(key, out var span))
            {
                span = GetSpan(exemplarPoint.TraceId, exemplarPoint.SpanId);
            }

            if (span != null)
            {
                newCache[key] = span;
            }

            var title = span != null
                ? SpanWaterfallViewModel.GetTitle(span, Applications)
                : $"Trace: {OtlpHelpers.ToShortenedId(exemplarPoint.TraceId)}";
            var tooltip = FormatTooltip(title, exemplarPoint.Value, exemplarPoint.Start);

            exemplarTraceDto.X.Add(exemplarPoint.Start);
            exemplarTraceDto.Y.Add(exemplarPoint.Value);
            exemplarTraceDto.Tooltips.Add(tooltip);
            exemplarTraceDto.TraceData.Add(new { TraceId = exemplarPoint.TraceId, SpanId = exemplarPoint.SpanId });
        }

        _traceCache = newCache;

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

    private OtlpSpan? GetSpan(string traceId, string spanId)
    {
        var trace = TelemetryRepository.GetTrace(traceId);
        if (trace == null)
        {
            return null;
        }

        return trace.Spans.FirstOrDefault(s => s.SpanId == spanId);
    }

    public void Dispose()
    {
        if (_chartInteropReference != null)
        {
            _chartInteropReference.Dispose();
            _chartInteropReference.Value.Dispose();
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
            var span = _plotlyChart.GetSpan(traceId, spanId);

            // Exemplar span isn't loaded yet. Display a dialog until the data is ready or the user cancels the dialog.
            if (span == null)
            {
                using var cts = new CancellationTokenSource();
                using var registration = _cts.Token.Register(cts.Cancel);

                var reference = await _plotlyChart.DialogService.ShowMessageBoxAsync(new DialogParameters<MessageBoxContent>()
                {
                    Content = new MessageBoxContent
                    {
                        Intent = MessageBoxIntent.Info,
                        Icon = new Icons.Filled.Size24.Info(),
                        IconColor = Color.Info,
                        Message = $"Waiting for trace {OtlpHelpers.ToShortenedId(traceId)} to load...",
                    },
                    DialogType = DialogType.MessageBox,
                    PrimaryAction = string.Empty,
                    SecondaryAction = "Cancel"
                });

                // Task that polls for the span to be available.
                var waitForTraceTask = Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        span = _plotlyChart.GetSpan(traceId, spanId);
                        if (span != null)
                        {
                            await reference.CloseAsync(DialogResult.Ok<bool>(true));
                        }
                        else
                        {
                            await Task.Delay(TimeSpan.FromSeconds(0.5), cts.Token);
                        }
                    }
                });

                var result = await reference.Result;
                cts.Cancel();

                await TaskHelpers.WaitIgnoreCancelAsync(waitForTraceTask);

                if (result.Cancelled)
                {
                    // Dialog was canceled before span was ready. Exit without navigating.
                    return;
                }
            }

            await _plotlyChart.InvokeAsync(() =>
            {
                _plotlyChart.NavigationManager.NavigateTo(DashboardUrls.TraceDetailUrl(traceId, spanId));
            });
        }
    }
}
