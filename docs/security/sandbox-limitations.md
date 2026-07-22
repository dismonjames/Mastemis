# Sandbox limitations

A production judge must execute candidate programs only through a supported Linux isolation implementation that disables network access, restricts capabilities and privilege escalation, enforces CPU, wall-time, memory, process, file, and output limits, kills descendants, and cleans its workspace. The current repository does not yet contain that runner, so candidate programs must not be executed by this build.

No sandbox should claim protection from every kernel or runtime exploit. Operators remain responsible for host patching, dedicated worker isolation, restricted credentials, resource monitoring, and incident response.
