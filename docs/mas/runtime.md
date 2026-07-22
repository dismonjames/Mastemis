# MAS runtime and determinism

The runtime uses version `mas-runtime-1.0` and the explicitly versioned `splitmix64-v1` generator. Equal source, seed, and runtime version produce equal input bytes; golden tests pin the random sequence. Bounded rejection removes modulo bias for integers. Floating values use a 53-bit fraction and invariant `G17` formatting.

Inputs use LF newlines and a final newline. Arrays use space-separated values. Trees and graphs begin with `node-count edge-count`, followed by one edge per line. The runtime enforces test, collection, graph, step, duration, and output limits, cancellation, bounded unique-generation attempts, and duplicate hashes. It has no filesystem, network, process, environment, reflection, native-library, or dynamic-code API.
