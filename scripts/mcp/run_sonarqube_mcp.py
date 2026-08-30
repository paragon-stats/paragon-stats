#!/usr/bin/env python3
"""Launch the SonarQube MCP server from a digest-pinned Docker image.

The image is pinned by digest, so it cannot change under us: an update is a
deliberate edit to IMAGE, reviewed like any other change. That matters here
because the container receives SONARQUBE_TOKEN and inherits stdio - it *is* the
MCP channel - so an unreviewed image would reach both the credential and the
agent.

A digest reference can never be stale, which is why there is no freshness
check: `docker run` already pulls an image that is missing locally, and that is
the only case left. stdout is reserved for the MCP stdio protocol; docker's own
pull progress goes to stderr on its own.

Docker invocation is overridable via MCP_DOCKER_CMD (e.g. "sudo docker").
"""

from __future__ import annotations

import os
import shlex
import subprocess

IMAGE = "mcp/sonarqube@sha256:925c88bc7cab2a1e1025b0bd43f0af504cd8ce1b99e9663ececaca914fb632e7"
DOCKER = shlex.split(os.environ.get("MCP_DOCKER_CMD", "docker") or "docker")


def main() -> int:
    # Inherited stdio: the container's stdin/stdout carry the MCP protocol.
    return subprocess.run(
        [*DOCKER, "run", "-i", "--rm", "--init", "-e", "SONARQUBE_TOKEN", "-e", "SONARQUBE_ORG", IMAGE],
        check=False,
    ).returncode


if __name__ == "__main__":
    raise SystemExit(main())
