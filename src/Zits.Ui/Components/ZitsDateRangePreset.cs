using Navius.Primitives.Common;

namespace Zits.Ui;

/// <summary>
/// A named quick-pick for <see cref="ZitsDateRangePicker"/>'s optional preset rail
/// (e.g. "Last 7 days"). Selecting it applies <see cref="Range"/> and closes the popup.
/// </summary>
public sealed record ZitsDateRangePreset(string Label, NaviusDateRange Range);
