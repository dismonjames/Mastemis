using Mastemis.Client.Core.Diagnostics;

namespace Mastemis.Client.VisualReview;

public sealed partial class VisualFixturePanel : UserControl
{
    public VisualFixturePanel() => InitializeComponent();

    public void SetReviewContext(VisualReviewOptions options) =>
        ContextText.Text = $"{options.Role} · {options.State} · {options.Theme}";
}
