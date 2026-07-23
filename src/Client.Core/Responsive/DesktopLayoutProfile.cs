namespace Mastemis.Client.Core.Responsive;

public enum DesktopWidthClass { Compact, Medium, Wide }

public sealed record DesktopLayoutProfile(
    DesktopWidthClass WidthClass,
    bool IsNavigationCompact,
    bool StackForms,
    bool StackDetailPanels,
    bool UseCompactProblemStudioNavigation)
{
    public static DesktopLayoutProfile Select(double width, double textScale = 1)
    {
        var effectiveWidth = width / Math.Clamp(textScale, 1, 2);
        var widthClass = effectiveWidth < 980 ? DesktopWidthClass.Compact
            : effectiveWidth < 1320 ? DesktopWidthClass.Medium : DesktopWidthClass.Wide;
        return new(widthClass, widthClass != DesktopWidthClass.Wide, widthClass == DesktopWidthClass.Compact,
            widthClass != DesktopWidthClass.Wide, widthClass == DesktopWidthClass.Compact);
    }
}
