#!/usr/bin/env python3
"""Launch the SonarQube MCP server, keeping its Docker image fresh efficiently.

Mirrors the image-freshness check the homelab repos use for Super-Linter
(`run_super_linter.py::_ensure_image_fresh`): compare the locally cached image's
repo digest to the registry manifest digest, pull only when stale (or missing),
and remove the superseded local image. This replaces `docker run --pull=always`
so we don't poll/prune on every MCP start by hand, and stale layers don't pile up.

stdout is reserved for the MCP stdio protocol, so all diagnostics go to stderr;
then we run `docker run` with inherited stdio so the container becomes the MCP
channel. Docker invocation is overridable via MCP_DOCKER_CMD (e.g. "sudo docker").
"""

from __future__ import annotations

import hashlib
import os
import shlex
import subprocess
import sys

IMAGE = "mcp/sonarqube"
DOCKER = shlex.split(os.environ.get("MCP_DOCKER_CMD", "docker") or "docker")


def _docker(args: list[str], *, capture: bool = False) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run([*DOCKER, *args], check=False, capture_output=capture)


def _local_repo_digest() -> str | None:
    """Repo digest of the locally cached image (e.g. 'sha256:...'), or None."""
    result = _docker(["image", "inspect", "--format", "{{index .RepoDigests 0}}", IMAGE], capture=True)
    if result.returncode != 0:
        return None
    raw = result.stdout.decode("utf-8", "replace").strip()
    return raw.split("@", 1)[1] if "@" in raw else None


def _remote_repo_digest() -> str | None:
    """Registry manifest digest, without downloading layers."""
    result = _docker(["buildx", "imagetools", "inspect", "--raw", IMAGE], capture=True)
    if result.returncode != 0:
        return None
    return "sha256:" + hashlib.sha256(result.stdout).hexdigest()


def _local_image_id() -> str | None:
    result = _docker(["image", "inspect", "--format", "{{.Id}}", IMAGE], capture=True)
    if result.returncode != 0:
        return None
    return result.stdout.decode("utf-8", "replace").strip() or None


def _pull() -> int:
    """Pull IMAGE; route docker's progress to stderr so stdout (the MCP channel) stays clean."""
    return subprocess.run([*DOCKER, "pull", IMAGE], stdout=sys.stderr, check=False).returncode


def _ensure_image_fresh() -> None:
    """Pull IMAGE only if missing or stale; remove the superseded image."""
    local = _local_repo_digest()
    if local is None:
        sys.stderr.write(f"{IMAGE} not cached locally; pulling...\n")
        _pull()
        return

    remote = _remote_repo_digest()
    if remote is None:
        sys.stderr.write(f"Cannot reach the registry; using cached {IMAGE}.\n")
        return
    if local == remote:
        return

    old_id = _local_image_id()
    sys.stderr.write(f"{IMAGE} is stale; pulling newer image...\n")
    if _pull() == 0 and old_id is not None:
        _docker(["rmi", old_id], capture=True)  # prune the superseded image by id


def main() -> int:
    _ensure_image_fresh()
    # Inherited stdio: the container's stdin/stdout carry the MCP protocol.
    return subprocess.run(
        [*DOCKER, "run", "-i", "--rm", "--init", "-e", "SONARQUBE_TOKEN", "-e", "SONARQUBE_ORG", IMAGE],
        check=False,
    ).returncode


if __name__ == "__main__":
    raise SystemExit(main())
