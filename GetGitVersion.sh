#!/usr/bin/bash

export PATH=${PATH}:/usr/bin

tag="$(git describe --tags)"
commitHash="$(git rev-parse HEAD)"

cat << EOF > Version.cs
namespace Git {
    class Commit {
        public static string VersionTag { get; set; } = "${tag}";
        public static string Hash { get; set; } = "${commitHash}";
    }
}
EOF

