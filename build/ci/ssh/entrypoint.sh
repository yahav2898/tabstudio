#!/usr/bin/env bash
set -euo pipefail
install -o developer -g developer -m 600 /authorized_key /home/developer/.ssh/authorized_keys
ssh-keygen -A
exec /usr/sbin/sshd -D -e
