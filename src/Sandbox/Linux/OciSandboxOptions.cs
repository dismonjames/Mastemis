namespace Mastemis.Sandbox.Linux;

public sealed record OciSandboxOptions(
    string RuntimePath = "/usr/bin/podman",
    string Image = "localhost/mastemis-judge:0.1.0",
    string ContainerUser = "1000:1000")
{
    public void Validate()
    {
        if (!Path.IsPathFullyQualified(RuntimePath) || string.IsNullOrWhiteSpace(Image) || Image.Length > 200 ||
            !ContainerUser.All(character => char.IsAsciiDigit(character) || character == ':'))
            throw new ArgumentException("OCI sandbox configuration is invalid.");
    }
}
