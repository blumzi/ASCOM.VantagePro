@echo on

echo "Writing Version.cs file..."

@rem Pushd/popd are used to temporarily cd to where the BAT file is.
@rem pushd $(ProjectDir)

@rem Verify that the command succeeds (i.e. Git is installed and we are in the repo).
echo loop0
set "git=C:\Program Files (x86)\Git\bin\git.exe"
@%git% rev-parse HEAD || exit 1

@rem Syntax for storing a command's output into a variable (see https://stackoverflow.com/a/2340018/492336).
@rem 'git rev-parse HEAD' returns the commit hash.
echo loop1
@rem for /f "delims=" %%i in ('"%git%" rev-parse HEAD') do set commitHash=%%i
echo loop2
@rem for /f "delims=" %%v in ('"%git%" describe --tags') do set versionTag=%%v

@rem Syntax for printing multiline text to a file (see https://stackoverflow.com/a/23530712/492336).
(
echo namespace Git
echo {
echo     class Commit
echo     {
echo         public static string VersionTag { get; set; } = ('"%git%" describe --tags');
echo         public static string Hash { get; set; } = ('"%git%" rev-parse HEAD');
echo     }
echo }
)> Version.cs

@rem popd  

exit 0
