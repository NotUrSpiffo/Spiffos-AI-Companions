#!/bin/sh
# Install this repository's commit-msg hook into the current clone only.
# Does not change global Git config and does not touch other repositories.
set -eu

usage() {
  echo "Usage: sh scripts/install-git-hooks.sh [--use-hooks-path] [--i-understand-this-ignores-dot-git-hooks]" >&2
  echo "Default: write .git/hooks/commit-msg only when that file is absent." >&2
  echo "--use-hooks-path: set local core.hooksPath to .githooks." >&2
  echo "That local setting makes Git ignore .git/hooks. If any non-sample hook" >&2
  echo "is already there, the script stops unless the second flag is also passed." >&2
}

mode=wrapper
acknowledge=0
for arg in "$@"; do
  case $arg in
    --use-hooks-path) mode=path ;;
    --i-understand-this-ignores-dot-git-hooks) acknowledge=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; usage; exit 2 ;;
  esac
done

root=$(git rev-parse --show-toplevel)
cd "$root"
versioned="$root/.githooks/commit-msg"
if [ ! -f "$versioned" ]; then
  echo "Missing $versioned" >&2
  exit 1
fi

if [ "$mode" = "wrapper" ]; then
  dest="$root/.git/hooks/commit-msg"
  if [ -e "$dest" ] || [ -L "$dest" ]; then
    echo "Refusing to overwrite $dest" >&2
    echo "Leave the existing hook in place and call this script from it:" >&2
    echo "  exec \"\$(git rev-parse --show-toplevel)/.githooks/commit-msg\" \"\$@\"" >&2
    exit 1
  fi
  mkdir -p "$root/.git/hooks"
  cat > "$dest" << 'EOF'
#!/bin/sh
exec "$(git rev-parse --show-toplevel)/.githooks/commit-msg" "$@"
EOF
  chmod +x "$dest" "$versioned"
  echo "Installed .git/hooks/commit-msg as a wrapper. Other hooks were not changed."
  echo "This setting is local to this clone. It was not written to global Git config."
  exit 0
fi

found=0
for f in "$root/.git/hooks"/*; do
  [ -e "$f" ] || [ -L "$f" ] || continue
  base=$(basename "$f")
  case $base in
    *.sample) continue ;;
  esac
  echo "Existing hook would be ignored: $base" >&2
  found=1
done
if [ "$found" -eq 1 ] && [ "$acknowledge" -ne 1 ]; then
  echo "Refusing to set core.hooksPath. Git would stop running the hooks listed above." >&2
  echo "Pass --i-understand-this-ignores-dot-git-hooks to set it anyway." >&2
  exit 1
fi

git config --local core.hooksPath .githooks
chmod +x "$versioned"
echo "Set local core.hooksPath to .githooks for this clone only."
if [ "$found" -eq 1 ]; then
  echo "Warning: hooks under .git/hooks will not run while that setting is in place."
fi
