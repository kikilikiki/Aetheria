#!/usr/bin/env python3
"""Construit aetheria-client_<version>_amd64.deb depuis aetheria-client-deb/.

Reconstruit le format .deb (ar + control.tar.gz + data.tar.gz) a la main, sans
dpkg-deb ni WSL - ni l'un ni l'autre n'est disponible sur la machine de build
Windows de ce projet (voir README.md, section "Paquet Linux"). Usage :

    python3 build-deb.py [version]   # ex: python3 build-deb.py 0.2.0

Attend que aetheria-client-deb/opt/aetheria/Aetheria.Client (et libglfw.so.3)
existent deja - voir README.md pour la commande `dotnet publish -r linux-x64`.
"""
import hashlib
import io
import os
import sys
import tarfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "aetheria-client-deb")
CONTROL_FILE = os.path.join(ROOT, "DEBIAN", "control")

EXECUTABLE_NAMES = {"Aetheria.Client", "aetheria"}


def mode_for(arcname):
    if os.path.basename(arcname) in EXECUTABLE_NAMES:
        return 0o755
    return 0o644


def build_data_tar():
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        dirs = ["./opt", "./opt/aetheria", "./usr", "./usr/bin", "./usr/share", "./usr/share/applications"]
        for d in dirs:
            ti = tarfile.TarInfo(name=d)
            ti.type = tarfile.DIRTYPE
            ti.mode = 0o755
            ti.uid = ti.gid = 0
            ti.uname = ti.gname = "root"
            ti.mtime = 0
            tar.addfile(ti)

        debian_dir = os.path.join(ROOT, "DEBIAN")
        files = []
        for base, _, names in os.walk(ROOT):
            if base.startswith(debian_dir):
                continue
            for n in names:
                full = os.path.join(base, n)
                rel = os.path.relpath(full, ROOT).replace(os.sep, "/")
                files.append((full, "./" + rel))

        for full, arcname in sorted(files, key=lambda p: p[1]):
            with open(full, "rb") as fh:
                data = fh.read()
            ti = tarfile.TarInfo(name=arcname)
            ti.size = len(data)
            ti.mode = mode_for(arcname)
            ti.uid = ti.gid = 0
            ti.uname = ti.gname = "root"
            ti.mtime = 0
            tar.addfile(ti, io.BytesIO(data))
    return buf.getvalue(), files


def build_control_tar(files, version):
    with open(CONTROL_FILE, "rb") as fh:
        control_content = fh.read()
    if version:
        lines = control_content.decode("utf-8").splitlines(keepends=True)
        lines = [f"Version: {version}\n" if line.startswith("Version:") else line for line in lines]
        control_content = "".join(lines).encode("utf-8")
    if not control_content.endswith(b"\n"):
        control_content += b"\n"

    md5_lines = []
    for full, arcname in sorted(files, key=lambda p: p[1]):
        with open(full, "rb") as fh:
            digest = hashlib.md5(fh.read()).hexdigest()
        md5_lines.append(f"{digest}  {arcname[2:]}\n")
    md5sums = "".join(md5_lines).encode("utf-8")

    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for name, content in [("./control", control_content), ("./md5sums", md5sums)]:
            ti = tarfile.TarInfo(name=name)
            ti.size = len(content)
            ti.mode = 0o644
            ti.uid = ti.gid = 0
            ti.uname = ti.gname = "root"
            ti.mtime = 0
            tar.addfile(ti, io.BytesIO(content))
    return buf.getvalue()


def ar_member_header(name, size):
    name_field = name.ljust(16)[:16]
    mtime_field = "0".ljust(12)
    uid_field = "0".ljust(6)
    gid_field = "0".ljust(6)
    mode_field = "100644".ljust(8)
    size_field = str(size).ljust(10)
    header = name_field + mtime_field + uid_field + gid_field + mode_field + size_field + "`\n"
    assert len(header) == 60
    return header.encode("ascii")


def build_ar(members):
    out = io.BytesIO()
    out.write(b"!<arch>\n")
    for name, data in members:
        out.write(ar_member_header(name, len(data)))
        out.write(data)
        if len(data) % 2 == 1:
            out.write(b"\n")
    return out.getvalue()


def main():
    version = sys.argv[1] if len(sys.argv) > 1 else None
    if not version:
        with open(CONTROL_FILE, encoding="utf-8") as fh:
            for line in fh:
                if line.startswith("Version:"):
                    version = line.split(":", 1)[1].strip()
                    break

    data_tar, files = build_data_tar()
    control_tar = build_control_tar(files, version)
    deb_bytes = build_ar([
        ("debian-binary", b"2.0\n"),
        ("control.tar.gz", control_tar),
        ("data.tar.gz", data_tar),
    ])

    out_path = os.path.join(HERE, f"aetheria-client_{version}_amd64.deb")
    with open(out_path, "wb") as fh:
        fh.write(deb_bytes)
    print(f"Wrote {out_path} ({len(deb_bytes)} bytes), {len(files)} data files")


if __name__ == "__main__":
    main()
