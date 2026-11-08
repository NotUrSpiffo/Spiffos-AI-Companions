# Commit message hook

`.githooks/commit-msg` removes a commit-message line only when it is a `Co-authored-by` trailer and the identity after the colon contains the letters `claude`, in any case. `Co-authored-by: Claude Opus 5.5 <noreply@anthropic.com>` and `Co-authored-by: CLAUDE <claude@example.com>` are removed. A line of prose that mentions Claude is kept. `Signed-off-by` is kept. Other `Co-authored-by` identities are kept.

The hook runs at `commit-msg` time. A `pre-commit` hook does not receive the message, so it cannot do this.

Git stores the message before this hook returns. The script rewrites the message file only when it removes a line. If nothing matches, the file is not opened for writing.

## Install in this clone

From Git Bash on Windows, or from a shell on Linux, at the repository root:

```sh
sh scripts/install-git-hooks.sh
```

That writes `.git/hooks/commit-msg` only when that file is absent. The new file calls `.githooks/commit-msg`. Other files in `.git/hooks` are left as they are. If `commit-msg` already exists, the installer stops and prints a line you can add to the existing hook. It does not replace that file.

To use Git's `core.hooksPath` instead:

```sh
sh scripts/install-git-hooks.sh --use-hooks-path
```

That sets `core.hooksPath` to `.githooks` in this clone's `.git/config` only. It does not change global Git config. While that setting is active, Git does not run hooks from `.git/hooks`. If a non-sample hook is already in `.git/hooks`, the installer stops. Passing `--i-understand-this-ignores-dot-git-hooks` as well records the local setting anyway and prints the hooks that will no longer run.

A fresh clone does not enable the hook by itself. Someone has to run the installer, or set `core.hooksPath` locally, in that clone.

## Limits

- Commits created on another machine, in the GitHub web editor, or by a tool that never runs this hook can still add the trailer.
- The hook does not rewrite commits that already exist.
- A `Co-authored-by` identity is matched by the substring `claude`. `NotClaude` in that trailer would also match.
- `core.hooksPath` applies to every hook name. This directory currently provides only `commit-msg`.
- The hook is a `/bin/sh` script using `awk`. Git for Windows runs repository hooks with its own shell, so a separate Python install is not required.
