using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.Errors;
public sealed partial class NotFoundPage : Page, IClientPage { public NotFoundPage() => InitializeComponent(); public ClientRoute Route => ClientRoute.NotFound; }
