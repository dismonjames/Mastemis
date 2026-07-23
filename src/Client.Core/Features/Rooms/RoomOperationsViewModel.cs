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
    private string invigilatorUserId = string.Empty;
    private RoomListItem? selectedRoom;
    private RoomScopeAssignment? selectedInvigilator;
    private bool isBusy;
    private string? error;
    public RoomOperationsViewModel(IRoomClient client) { this.client = client; CreateCommand = new AsyncCommand(CreateAsync); RefreshCommand = new AsyncCommand(RefreshAsync); LoadInvigilatorsCommand = new AsyncCommand(LoadInvigilatorsAsync); AssignInvigilatorCommand = new AsyncCommand(AssignInvigilatorAsync); RemoveInvigilatorCommand = new AsyncCommand(RemoveInvigilatorAsync); }
    public ICommand CreateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LoadInvigilatorsCommand { get; }
    public ICommand AssignInvigilatorCommand { get; }
    public ICommand RemoveInvigilatorCommand { get; }
    public ObservableCollection<RoomSummary> CreatedRooms { get; } = [];
    public ObservableCollection<RoomListItem> Rooms { get; } = [];
    public ObservableCollection<RoomScopeAssignment> Invigilators { get; } = [];
    public string ExamId { get => examId; set => SetProperty(ref examId, value); }
    public string Name { get => name; set => SetProperty(ref name, value); }
    public string InvigilatorUserId { get => invigilatorUserId; set => SetProperty(ref invigilatorUserId, value); }
    public RoomListItem? SelectedRoom { get => selectedRoom; set => SetProperty(ref selectedRoom, value); }
    public RoomScopeAssignment? SelectedInvigilator { get => selectedInvigilator; set => SetProperty(ref selectedInvigilator, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool HasRooms => CreatedRooms.Count > 0;
    public bool HasLoadedRooms => Rooms.Count > 0;
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Error = null; if (!Guid.TryParse(ExamId, out var id)) { Error = "Enter a valid examination ID."; return; }
        IsBusy = true; try { var page = await client.ListAsync(id, null, cancellationToken).ConfigureAwait(true); Rooms.Clear(); foreach (var room in page?.Items ?? []) Rooms.Add(room); OnPropertyChanged(nameof(HasLoadedRooms)); }
        catch (ApiException value) { Error = value.Problem.Title; }
        finally { IsBusy = false; }
    }
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        Error = null;
        if (!Guid.TryParse(ExamId, out var id) || string.IsNullOrWhiteSpace(Name)) { Error = "Enter an examination ID and room name."; return; }
        IsBusy = true;
        try { CreatedRooms.Insert(0, await client.CreateAsync(id, Name.Trim(), cancellationToken).ConfigureAwait(true)); OnPropertyChanged(nameof(HasRooms)); Name = string.Empty; }
        catch (ApiException value) { Error = value.Problem.Title; }
        finally { IsBusy = false; }
    }
    private async Task LoadInvigilatorsAsync(CancellationToken ct) { Error = null; if (SelectedRoom is null) { Error = "Select a room."; return; } try { var values = await client.ListInvigilatorsAsync(SelectedRoom.Id, ct); Invigilators.Clear(); foreach (var value in values) Invigilators.Add(value); } catch (ApiException value) { Error = value.Problem.Title; } }
    private async Task AssignInvigilatorAsync(CancellationToken ct) { Error = null; if (SelectedRoom is null || !Guid.TryParse(InvigilatorUserId, out var userId)) { Error = "Select a room and enter a valid user ID."; return; } try { await client.AssignInvigilatorAsync(SelectedRoom.Id, userId, ct); await LoadInvigilatorsAsync(ct); } catch (ApiException value) { Error = value.Problem.Title; } }
    private async Task RemoveInvigilatorAsync(CancellationToken ct) { Error = null; if (SelectedRoom is null || SelectedInvigilator is null) return; try { await client.RemoveInvigilatorAsync(SelectedRoom.Id, SelectedInvigilator.UserId, ct); await LoadInvigilatorsAsync(ct); } catch (ApiException value) { Error = value.Problem.Title; } }
}
