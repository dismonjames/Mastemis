using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.ReferenceSolution;

public sealed class ReferenceSourceDocument : ObservableObject
{
    private string fileName, content;
    public ReferenceSourceDocument(string fileName, string content) { this.fileName = fileName; this.content = content; }
    public string FileName { get => fileName; set => SetProperty(ref fileName, value); }
    public string Content { get => content; set => SetProperty(ref content, value); }
}

public sealed class ReferenceSolutionViewModel : ObservableObject
{
    private readonly IReferenceSolutionClient client; private Guid problemId; private ReferenceSourceDocument? selected;
    private string language = "cpp", newFileName = "main.cpp", status = "Select a draft", error = string.Empty;
    public ReferenceSolutionViewModel(IReferenceSolutionClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); SaveCommand = new AsyncCommand(SaveAsync); AddFileCommand = new AsyncCommand(_ => { AddFile(); return Task.CompletedTask; }); RemoveFileCommand = new AsyncCommand(_ => { RemoveFile(); return Task.CompletedTask; }); }
    public ObservableCollection<ReferenceSourceDocument> Sources { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand AddFileCommand { get; }
    public ICommand RemoveFileCommand { get; }
    public ReferenceSourceDocument? Selected { get => selected; set => SetProperty(ref selected, value); }
    public string Language { get => language; set => SetProperty(ref language, value); }
    public string NewFileName { get => newFileName; set => SetProperty(ref newFileName, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; Sources.Clear(); Status = "Ready"; }
    private void AddFile() { if (string.IsNullOrWhiteSpace(NewFileName) || Sources.Any(x => x.FileName.Equals(NewFileName, StringComparison.OrdinalIgnoreCase))) return; var source = new ReferenceSourceDocument(NewFileName.Trim(), string.Empty); Sources.Add(source); Selected = source; }
    private void RemoveFile() { if (Selected is not null) Sources.Remove(Selected); Selected = Sources.FirstOrDefault(); }
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var revision = await client.GetAsync(problemId, ct); Sources.Clear(); if (revision is null) { Status = "No reference solution"; return; } Language = revision.Language; foreach (var item in revision.Sources) { await using var stream = await client.OpenSourceAsync(problemId, revision.RevisionId, item.FileName, ct); using var reader = new StreamReader(stream, Encoding.UTF8); Sources.Add(new(item.FileName, await reader.ReadToEndAsync(ct))); } Selected = Sources.FirstOrDefault(); Status = $"Revision {revision.RevisionId:D}"; });
    private async Task SaveAsync(CancellationToken ct) => await RunAsync(async () => { if (Language is not ("cpp" or "csharp")) throw new InvalidOperationException("Only C++ and C# are supported."); var values = Sources.Select(x => new ReferenceSourceUpdate(x.FileName, Convert.ToBase64String(Encoding.UTF8.GetBytes(x.Content)))).ToArray(); var revision = await client.SaveAsync(problemId, Language, values, ct); Status = revision is null ? "Save failed" : $"Saved revision {revision.RevisionId:D}"; });
    private async Task RunAsync(Func<Task> action) { Error = string.Empty; try { await action(); } catch (ApiException ex) { Error = ex.Problem.Title; } catch (Exception ex) when (ex is InvalidOperationException or IOException) { Error = ex.Message; } }
}
