using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Candidates;

public sealed class CandidateOperationsViewModel : ObservableObject
{
    private readonly ICandidateClient client;
    private string examId = string.Empty;
    private string userId = string.Empty;
    private string registrationCode = string.Empty;
    private bool isBusy;
    private string? error;
    public CandidateOperationsViewModel(ICandidateClient client) { this.client = client; RegisterCommand = new AsyncCommand(RegisterAsync); RefreshCommand = new AsyncCommand(RefreshAsync); }
    public ICommand RegisterCommand { get; }
    public ICommand RefreshCommand { get; }
    public ObservableCollection<CandidateRegistration> Registrations { get; } = [];
    public ObservableCollection<CandidateListItem> Candidates { get; } = [];
    public string ExamId { get => examId; set => SetProperty(ref examId, value); }
    public string UserId { get => userId; set => SetProperty(ref userId, value); }
    public string RegistrationCode { get => registrationCode; set => SetProperty(ref registrationCode, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool HasRegistrations => Registrations.Count > 0;
    public bool HasCandidates => Candidates.Count > 0;
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Error = null; if (!Guid.TryParse(ExamId, out var exam)) { Error = "Enter a valid examination ID."; return; }
        IsBusy = true; try { var page = await client.ListAsync(exam, null, cancellationToken).ConfigureAwait(true); Candidates.Clear(); foreach (var item in page?.Items ?? []) Candidates.Add(item); OnPropertyChanged(nameof(HasCandidates)); }
        catch (ApiException value) { Error = value.Problem.Title; } finally { IsBusy = false; }
    }
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        Error = null;
        if (!Guid.TryParse(ExamId, out var exam) || !Guid.TryParse(UserId, out var user) || string.IsNullOrWhiteSpace(RegistrationCode)) { Error = "Enter valid examination and user IDs plus a registration code."; return; }
        IsBusy = true;
        try { Registrations.Insert(0, await client.RegisterAsync(exam, user, RegistrationCode.Trim(), cancellationToken).ConfigureAwait(true)); OnPropertyChanged(nameof(HasRegistrations)); RegistrationCode = string.Empty; }
        catch (ApiException value) { Error = value.Problem.Title; }
        finally { IsBusy = false; }
    }
}
