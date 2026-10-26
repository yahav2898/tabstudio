FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update && apt-get install -y --no-install-recommends openssh-server python3 sudo \
    && useradd --create-home --shell /bin/bash developer \
    && passwd -d developer \
    && mkdir -p /run/sshd /home/developer/.ssh /home/developer/workspaces \
    && chmod 700 /home/developer/.ssh \
    && chown -R developer:developer /home/developer \
    && printf '%s\n' 'PasswordAuthentication no' 'KbdInteractiveAuthentication no' 'AllowTcpForwarding yes' 'PermitRootLogin no' 'HostKeyAlgorithms ssh-ed25519' >> /etc/ssh/sshd_config
COPY entrypoint.sh /entrypoint.sh
ENTRYPOINT ["bash", "/entrypoint.sh"]
