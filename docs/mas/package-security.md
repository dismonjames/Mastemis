# Package security

Package inspection buffers only within configured compressed-size limits and streams every entry through an independent bound. It rejects traversal, absolute/drive/UNC paths, backslashes, nulls, unsafe segments, symlink entries, case and Unicode-normalization collisions, excessive entries, path length, entry size, total expansion, compression ratio, malformed archives, missing manifests, unsupported versions, bad references, invalid judge policy, and checksum mismatches.

Raw ZIP feature inspection rejects encryption and strong-encryption flags, methods other than stored/deflate, local/central header disagreements, unexpected trailing data, and nested ZIP-signature content. Unknown required manifest features and unreferenced files in critical directories fail closed. These checks cover what the .NET ZIP representation and bounded raw metadata inspection expose; they are not a claim of perfect ZIP parser security.

Archives are never extracted to an uncontrolled directory. Production CreateNew and ReplaceDraft workflows consume only inspected entries, map package paths to generated object identifiers, stage bounded content, and publish relational state only after validation. Package signatures remain reserved metadata and are not verified in version 1.0.
