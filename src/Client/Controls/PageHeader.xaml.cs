namespace Mastemis.Client.Controls;

public sealed partial class PageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty EyebrowProperty = DependencyProperty.Register(
        nameof(Eyebrow), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(
        nameof(Commands), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader() => InitializeComponent();
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string Eyebrow { get => (string)GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }
    public object? Commands { get => GetValue(CommandsProperty); set => SetValue(CommandsProperty, value); }
    public Visibility SubtitleVisibility => string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EyebrowVisibility => string.IsNullOrWhiteSpace(Eyebrow) ? Visibility.Collapsed : Visibility.Visible;
}
