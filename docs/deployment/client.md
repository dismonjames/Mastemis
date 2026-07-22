# Client build and operation

Restore and build the desktop client from the repository root:

```bash
dotnet restore Mastemis.sln
dotnet build src/Client/Mastemis.Client.csproj -c Release
dotnet run --project src/Client/Mastemis.Client.csproj -c Release -f net10.0-desktop
```

Linux desktop startup requires a usable X11 or framebuffer environment and the native libraries required by Uno Skia.

For WebAssembly, first install the .NET WebAssembly workload, then build explicitly:

```bash
dotnet workload install wasm-tools
dotnet build src/Client/Mastemis.Client.csproj -c Release -p:MastemisBuildWebAssembly=true -f net10.0-browserwasm
```

The workload may require administrator-managed SDK installation. A build fails closed when it is absent; the repository does not silently omit browser-native assets from a requested WebAssembly build.

At first launch, select Host or Connect Mode and enter the operator-controlled Mastemis server URL. HTTPS is required outside loopback development. Login uses the server's secure Identity cookie and antiforgery contract. The application does not contain a default account or password.

Known limitations: runtime desktop/browser smoke tests require a graphical/browser environment; several operational pages await complete backend list/query endpoints; the source and MAS editors are accessible multiline editors rather than full language-aware editors; browser SFE collection is outside this milestone.
