#!/usr/bin/bash

export PATH=${PATH}:/usr/bin

versionTag="$(git describe --tags --abbrev=0)"
commitHash="$(git rev-parse HEAD)"
originUrl="$(git remote get-url --push origin)"

cat << EOF > Version.cs
namespace Git {
    class Latest {
        public static string VersionTag { get; set; } = "${versionTag}";
        public static string CommitHash { get; set; } = "${commitHash}";
        public static string OriginUrl { get; set; } = "${originUrl%.git}/releases/tag/${versionTag}";
    }
}
EOF
