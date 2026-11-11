#!/usr/bin/env python3
"""Build the distribution zip from one verified git commit.

Binary, example config, translation, and license bytes are read from that
commit. They are not taken from an older release zip or from a different
plugin version.
"""
import hashlib
import subprocess
import sys
import zipfile
from pathlib import Path

PREFIX = "Spiffo's AI Companions/"
COMMIT_FILES = {
    "instalar/BepInEx/plugins/SpiffoAiCompanions.dll":
        "install/BepInEx/plugins/SpiffoAiCompanions.dll",
    "instalar/BepInEx/plugins/OfflineCompanionsReborn/Translations/Spanish.json":
        "install/BepInEx/plugins/OfflineCompanionsReborn/Translations/Spanish.json",
    "instalar/BepInEx/plugins/OfflineCompanionsReborn/Translations/speech/Spanish.json":
        "install/BepInEx/plugins/OfflineCompanionsReborn/Translations/speech/Spanish.json",
    "instalar/BepInEx/plugins/OfflineCompanionsReborn/Translations/Russian.json":
        "install/BepInEx/plugins/OfflineCompanionsReborn/Translations/Russian.json",
    "instalar/BepInEx/plugins/OfflineCompanionsReborn/Translations/speech/Russian.json":
        "install/BepInEx/plugins/OfflineCompanionsReborn/Translations/speech/Russian.json",
    "configuracion/SpiffoAiCompanions.cfg":
        "configuration examples/SpiffoAiCompanions.cfg",
    "configuracion/Maddhattr.OfflineCompanionsReborn.cfg":
        "configuration examples/Maddhattr.OfflineCompanionsReborn.cfg",
    "licenses/Offline-Companions-Reborn-MIT.txt":
        "licenses/Offline-Companions-Reborn-MIT.txt",
    "THIRD_PARTY_NOTICES.txt":
        "licenses/THIRD_PARTY_NOTICES.txt",
}


def git_bytes(commit: str, path: str) -> bytes:
    return subprocess.check_output(["git", "show", f"{commit}:{path}"])


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: build-distribution-zip.py <commit> <output.zip>", file=sys.stderr)
        return 2
    commit, dest_s = sys.argv[1], sys.argv[2]
    dest = Path(dest_s)
    root = Path(__file__).resolve().parents[1]
    texts = {
        "README.txt": (root / "packaging" / "README.txt").read_bytes(),
        "LEEME.txt": (root / "packaging" / "LEEME.txt").read_bytes(),
        "RELEASE_NOTES.txt": (root / "packaging" / "RELEASE_NOTES.txt").read_bytes(),
        "configuration examples/README.txt":
            (root / "packaging" / "configuration-examples-README.txt").read_bytes(),
    }
    for name, data in texts.items():
        if b"not been tested" in data.lower() or "no se han probado" in data.decode("utf-8").lower():
            print("refusing untested-in-game wording in " + name, file=sys.stderr)
            return 1
        data.decode("utf-8")
    payloads = {dst: git_bytes(commit, src) for src, dst in COMMIT_FILES.items()}
    dll = payloads["install/BepInEx/plugins/SpiffoAiCompanions.dll"]
    if b"\x060.27.2" not in dll or b"0.27.1" in dll:
        print("DLL is not the 0.27.2 assembly", file=sys.stderr)
        return 1
    payloads.update(texts)
    dest.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(dest, "w", compression=zipfile.ZIP_DEFLATED) as zout:
        for rel in sorted(payloads):
            zout.writestr(PREFIX + rel, payloads[rel])
    raw = dest.read_bytes()
    with zipfile.ZipFile(dest) as check:
        names = []
        for info in check.infolist():
            if info.is_dir():
                continue
            rel = info.filename[len(PREFIX):]
            names.append(rel)
            if rel.endswith(".md") or rel.startswith("codigo/") or rel.startswith("docs/") or rel == "SHA256SUMS":
                print("forbidden path " + rel, file=sys.stderr)
                return 1
            if check.read(info.filename) != payloads[rel]:
                print("mismatch " + rel, file=sys.stderr)
                return 1
    print("files", len(names))
    for rel in sorted(names):
        print(" ", rel)
    print("zip_sha256", hashlib.sha256(raw).hexdigest())
    print("zip_bytes", len(raw))
    print("dll_sha256", hashlib.sha256(dll).hexdigest())
    print("dll_bytes", len(dll))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
