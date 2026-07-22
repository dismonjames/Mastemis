using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Rooms;

public sealed class RoomOperationsViewModel : ObservableObject
{
    private readonly IRoomClient client;
    private string examId = string.Empty;
    private string name = string.Empty;
    private bool isBusy;
    private string? error;
    public RoomOperationsViewModel(IRoomClient client) { this.client = client; CreateCommand = new AsyncCommand(CreateAsync); }
    public ICommand CreateCommand { get; }
    public ObservableCollection<RoomSummary> CreatedRooms { get; } = [];
    public string ExamId { get => examId; set => SetProperty(ref examId, value); }
    public string Name { get => name; set => SetProperty(ref name, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool HasRooms => CreatedRooms.Count > 0;
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        Error = null;
        if (!Guid.TryParse(ExamId, out var id) || string.IsNullOrWhiteSpace(Name)) { Error = "Enter an examination ID and room name."; return; }
        IsBusy = true;
        try { CreatedRooms.Insert(0, await client.CreateAsync(id, Name.Trim(), cancellationToken).ConfigureAwait(true)); OnPropertyChanged(nameof(HasRooms)); Name = string.Empty; }
        catch (ApiException value) { Error = value.Problem.Title; }
        finally { IsBusy = false; }
    }
}
