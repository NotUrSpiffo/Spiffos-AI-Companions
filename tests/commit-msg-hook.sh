#!/bin/sh
# Exercise .githooks/commit-msg without installing it into this clone.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
hook="$root/.githooks/commit-msg"
install="$root/scripts/install-git-hooks.sh"
fail=0

if [ ! -f "$hook" ] || [ ! -f "$install" ]; then
  echo "missing hook or installer" >&2
  exit 1
fi

tmpdir=$(mktemp -d "${TMPDIR:-/tmp}/commit-msg-test.XXXXXX")
trap 'rm -rf "$tmpdir"' EXIT
work="$tmpdir/dir with spaces"
mkdir -p "$work"

check() {
  name=$1
  if eval "$2"; then
    echo "ok  $name"
  else
    echo "FAIL $name" >&2
    fail=1
  fi
}

run_hook() {
  sh "$hook" "$1"
}

# Claude and CLAUDE trailers go away. Another co-author and prose stay.
msg="$work/message one.txt"
printf '%s\n' \
  'Subject' \
  '' \
  'Prose that mentions Claude stays in the body.' \
  'Co-authored-by: Claude Opus 5.5 <noreply@anthropic.com>' \
  'Co-authored-by: Ada Lovelace <ada@example.com>' \
  'Co-authored-by: CLAUDE <claude@example.com>' \
  'Signed-off-by: Claude Reviewer <review@example.com>' \
  > "$msg"
run_hook "$msg"
check "mixed trailers" "grep -q 'Prose that mentions Claude' '$msg' \
  && grep -q 'Ada Lovelace' '$msg' \
  && grep -q 'Signed-off-by: Claude Reviewer' '$msg' \
  && ! grep -i -q '^Co-authored-by:.*[Cc]laude' '$msg'"

# Several Claude trailers, surrounding co-authors unchanged.
msg="$work/several.txt"
printf '%s\n' \
  'Co-authored-by: First Person <first@example.com>' \
  'Co-authored-by: Claude <a@b.c>' \
  'Co-authored-by: Second Person <second@example.com>' \
  'Co-authored-by: Bot <claude@example.com>' \
  'Co-authored-by: Third Person <third@example.com>' \
  > "$msg"
run_hook "$msg"
check "multiple trailers" "printf '%s\n' \
  'Co-authored-by: First Person <first@example.com>' \
  'Co-authored-by: Second Person <second@example.com>' \
  'Co-authored-by: Third Person <third@example.com>' \
  | cmp - '$msg'"

# No match: bytes stay identical, including a missing final newline.
msg="$work/unchanged.txt"
printf 'Just a subject' > "$msg"
cp "$msg" "$work/unchanged.before"
run_hook "$msg"
check "no match untouched" "cmp '$work/unchanged.before' '$msg'"

# CRLF lines keep CRLF after a Claude trailer is removed.
msg="$work/crlf.txt"
printf 'Subject\r\n\r\nKept prose about Claude.\r\nCo-authored-by: Claude <a@b.c>\r\nCo-authored-by: Ada <ada@example.com>\r\n' > "$msg"
run_hook "$msg"
printf 'Subject\r\n\r\nKept prose about Claude.\r\nCo-authored-by: Ada <ada@example.com>\r\n' > "$work/crlf.expected"
check "crlf preserved" "cmp '$work/crlf.expected' '$msg'"

# Installer does not overwrite an existing commit-msg and does not set global config.
repo="$tmpdir/sample repo"
mkdir -p "$repo"
git -C "$repo" init -q
mkdir -p "$repo/.git/hooks" "$repo/.githooks" "$repo/scripts"
cp "$hook" "$repo/.githooks/commit-msg"
cp "$install" "$repo/scripts/install-git-hooks.sh"
printf '#!/bin/sh\necho existing\n' > "$repo/.git/hooks/commit-msg"
chmod +x "$repo/.git/hooks/commit-msg"
set +e
(cd "$repo" && sh scripts/install-git-hooks.sh) >/tmp/commit-msg-install.out 2>/tmp/commit-msg-install.err
install_status=$?
set -e
check "installer refuses overwrite" "[ '$install_status' -eq 1 ] && grep -q 'existing' '$repo/.git/hooks/commit-msg'"
check "no global hooksPath" "! git config --global --get core.hooksPath >/dev/null 2>&1"

clean="$tmpdir/clean repo"
mkdir -p "$clean"
git -C "$clean" init -q
mkdir -p "$clean/.githooks" "$clean/scripts"
cp "$hook" "$clean/.githooks/commit-msg"
cp "$install" "$clean/scripts/install-git-hooks.sh"
(cd "$clean" && sh scripts/install-git-hooks.sh)
printf 'Co-authored-by: Claude <a@b.c>\nCo-authored-by: Ada <ada@example.com>\n' > "$clean/msg.txt"
(cd "$clean" && sh .git/hooks/commit-msg msg.txt)
check "wrapper strips trailer" "grep -q 'Ada' '$clean/msg.txt' && ! grep -q 'Claude' '$clean/msg.txt'"
set +e
(cd "$clean" && sh scripts/install-git-hooks.sh --use-hooks-path) >/tmp/commit-msg-path.out 2>/tmp/commit-msg-path.err
path_status=$?
set -e
check "hooksPath refused while wrapper exists" "[ '$path_status' -eq 1 ]"

if [ "$fail" -ne 0 ]; then
  echo "commit-msg hook tests failed" >&2
  exit 1
fi
echo "all commit-msg hook tests passed"
