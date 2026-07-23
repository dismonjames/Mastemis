namespace Mastemis.Client.Pages.ProblemStudio.Sections;

public sealed partial class PackagesSection : UserControl
{
    public PackagesSection() => InitializeComponent();
    private void Package_DragOver(object sender, DragEventArgs e) { e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy; e.DragUIOverride.Caption = "Import Mastemis package"; }
    private async void Package_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not Core.Features.ProblemStudio.ProblemStudioViewModel model || !e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 1) await model.Packages.AcceptDroppedAsync(items[0], CancellationToken.None);
    }
}
