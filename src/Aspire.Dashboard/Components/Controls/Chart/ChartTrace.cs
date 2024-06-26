// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Aspire.Dashboard.Otlp.Model;

namespace Aspire.Dashboard.Components.Controls.Chart;

public sealed class ChartTrace
{
    public int? Percentile { get; init; }
    public required string Name { get; init; }
    public List<double?> Values { get; } = new();
    public List<double?> DiffValues { get; } = new();
    public List<string?> Tooltips { get; } = new();
}

[DebuggerDisplay("Start = {Start}, Value = {Value}, TraceId = {TraceId}, SpanId = {SpanId}")]
public class Exemplar
{
    public DateTimeOffset Start { get; init; }
    public double Value { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public OtlpSpan? Span { get; init; }
}
