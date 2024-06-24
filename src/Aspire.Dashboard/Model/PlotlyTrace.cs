// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Model;

public class PlotlyTrace
{
    public required string Name { get; init; }
    public required List<double?> Values { get; init; }
    public required List<string?> Tooltips { get; init; }
}

public class PlotlyExemplar
{
    public required DateTimeOffset Start { get; init; }
    public required double Value { get; init; }
    public required string? TraceId { get; init; }
    public required string? SpanId { get; init; }
    public required string Tooltip { get; init; }
}

public class PlotlyUserLocale
{
    public required string Time { get; init; }
    public required List<string> Periods { get; init; }
}
