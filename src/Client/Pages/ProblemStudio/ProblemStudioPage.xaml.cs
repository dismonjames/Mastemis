using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.ProblemStudio;

public sealed partial class ProblemStudioPage : Page, IClientPage
{
    public ProblemStudioPage(ProblemStudioViewModel vm) { InitializeComponent(); DataContext = vm; }
    public ClientRoute Route => ClientRoute.ProblemStudio;
    private ProblemStudioViewModel ViewModel => (ProblemStudioViewModel)DataContext;
    private void MasEditor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var offset = Math.Clamp(MasEditor.SelectionStart, 0, MasEditor.Text.Length);
        var before = MasEditor.Text[..offset]; var line = before.Count(x => x == '\n') + 1;
        var last = before.LastIndexOf('\n'); ViewModel.SetCursor(line, offset - last);
    }
    private void Diagnostic_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not MasDiagnostic { Line: { } line, Column: { } column }) return;
        var lines = MasEditor.Text.Split('\n'); var offset = lines.Take(Math.Max(0, line - 1)).Sum(x => x.Length + 1) + Math.Max(0, column - 1);
        MasEditor.SelectionStart = Math.Min(offset, MasEditor.Text.Length); MasEditor.Focus(FocusState.Programmatic);
    }
}
