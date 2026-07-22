# Candidate code threat model

Candidate programs are hostile. Expected attacks include CPU and memory exhaustion, fork bombs, endless output, oversized files, background children, network scans, environment discovery, filesystem traversal, compiler argument injection, malformed output, and exploitation of compiler, runtime, container engine, or kernel defects.

Mastemis reduces these risks with immutable job inputs, generated host paths, structured process arguments, compiler and output bounds, a minimal non-root image, disabled network, read-only base filesystem, dropped capabilities, no-new-privileges, cgroup/ulimit controls, process-tree cleanup, lease recovery, and dedicated workers. The API server never executes submissions.

Residual trust remains in the Linux kernel, Podman, OCI runtime, toolchains, and configured image. Container isolation is not a formal proof of safety and does not cover unknown kernel escape vulnerabilities. Operators must patch hosts and images, avoid privileged containers and runtime sockets, isolate worker networks from databases and control planes, constrain host resources, monitor failures, rotate credentials, and rebuild pinned images through a trusted supply chain.
