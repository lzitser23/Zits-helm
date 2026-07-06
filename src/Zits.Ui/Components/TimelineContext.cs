namespace Zits.Ui;

/// <summary>
/// Cascaded from a <c>ZitsTimeline</c> to its item / separator / connector parts so they lay out
/// along the shared axis (Vertical or Horizontal) and side (Left / Right / Alternate) without
/// re-declaring the orientation per part.
/// </summary>
public sealed record TimelineContext(string Orientation, string Align);
