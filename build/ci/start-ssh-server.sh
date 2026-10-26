#!/usr/bin/env bash
# CI's isolated OpenSSH fixture. Only a public key is mounted into the server; no password login or external bind.
set -euo pipefail
mkdir -p ci-out/ssh
fixture="${RUNNER_TEMP:-/tmp}/tabstudio-ssh-${GITHUB_JOB:-local}"
mkdir -p "$fixture"
ssh-keygen -q -t rsa -b 3072 -m PEM -N '' -f "$fixture/id_rsa"
docker build -t tabstudio-openssh build/ci/ssh
docker run -d --name tabstudio-openssh -p 127.0.0.1:2222:22 \
  -v "$fixture/id_rsa.pub:/authorized_key:ro" tabstudio-openssh
for attempt in $(seq 1 60); do
  if ssh-keyscan -t ed25519 -p 2222 127.0.0.1 > ci-out/ssh/known_hosts 2>/dev/null && [ -s ci-out/ssh/known_hosts ]; then break; fi
  sleep 1
done
test -s ci-out/ssh/known_hosts
fingerprint=$(ssh-keygen -lf ci-out/ssh/known_hosts -E sha256 | awk '{print $2}')
{
  echo 'TABSTUDIO_SSH_HOST=127.0.0.1'
  echo 'TABSTUDIO_SSH_PORT=2222'
  echo 'TABSTUDIO_SSH_USER=developer'
  echo 'TABSTUDIO_SSH_ROOT=/home/developer/workspaces'
  echo "TABSTUDIO_SSH_IDENTITY=$fixture/id_rsa"
  echo "TABSTUDIO_SSH_FINGERPRINT=$fingerprint"
} >> "${GITHUB_ENV:?Run this in CI, or set GITHUB_ENV to a local environment file.}"
