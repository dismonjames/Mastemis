# Package security

Package inspection buffers only within configured compressed-size limits and streams every entry through an independent bound. It rejects traversal, absolute/drive/UNC paths, backslashes, nulls, unsafe segments, symlink entries, case and Unicode-normalization collisions, excessive entries, path length, entry size, total expansion, compression ratio, malformed archives, missing manifests, unsupported versions, bad references, invalid judge policy, and checksum mismatches.

Archives are never extracted to an uncontrolled directory. Current import is inspection/validation only: production relational import, staged object cleanup, replacement policy, encrypted-entry detection, and signature verification remain incomplete.
