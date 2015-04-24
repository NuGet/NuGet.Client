# Temporary "strawman" nuget3 client
Not a prototype, just not in the final location yet :)

**NOTE:** after cloning, and whenever you switch branches/pull you must run `git submodule update --init` to ensure you have the latest copy of the submodules.

## Usage:

1. `git submodule update --init` at the root
1. `dnu restore` at the root (Ignore errors about missing "Microsoft.NETCore.Runtime")
1. `.\nuget3` at the root

You need to specify **ALL** sources for `restore`. It does **not** read from `nuget.config` files yet. It also does not yet handle project-to-project csproj dependencies 
but that is coming soon. You need to use a source that has the latest packaging conventions (`runtimes/` folders) in order to see the whole restore work.
