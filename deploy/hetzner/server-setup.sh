#!/usr/bin/env bash
# One-time setup of a fresh Ubuntu 22.04/24.04 server (run as root): updates, firewall, Docker, automatic security updates.
# Before you run it: add your SSH key to the server from the Hetzner console and check you can sign in with it.
# After: sign in as the `deploy` user (same key) and follow RUNBOOK.md.
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then echo "Run as root."; exit 1; fi

apt-get update
DEBIAN_FRONTEND=noninteractive apt-get -y upgrade
DEBIAN_FRONTEND=noninteractive apt-get install -y ca-certificates curl git ufw fail2ban unattended-upgrades rclone

# Docker (official repository)
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
. /etc/os-release
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" > /etc/apt/sources.list.d/docker.list
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# A normal user for the daily work, with the root SSH key copied to it
id deploy >/dev/null 2>&1 || adduser --disabled-password --gecos "" deploy
usermod -aG docker deploy
mkdir -p /home/deploy/.ssh
cp /root/.ssh/authorized_keys /home/deploy/.ssh/authorized_keys
chown -R deploy:deploy /home/deploy/.ssh
chmod 700 /home/deploy/.ssh && chmod 600 /home/deploy/.ssh/authorized_keys

# SSH: keys only, no root password login
printf 'PasswordAuthentication no
PermitRootLogin prohibit-password
' > /etc/ssh/sshd_config.d/00-inspecta.conf
sshd -t && (systemctl reload ssh || systemctl reload sshd)
sshd -T | grep -iE "^(passwordauthentication|permitrootlogin)"

# Firewall: only SSH and the web ports. (Docker publishes 80/443 itself; the database has no published port.)
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 443/udp
ufw --force enable

# Docker logs must not fill the disk
mkdir -p /etc/docker
printf '{"log-driver":"json-file","log-opts":{"max-size":"10m","max-file":"5"}}
' > /etc/docker/daemon.json
systemctl restart docker

# Automatic security updates and a small swap file (the first build of the API needs memory)
dpkg-reconfigure -f noninteractive unattended-upgrades
if [ ! -f /swapfile ]; then
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

timedatectl set-timezone Africa/Cairo
echo "Done. Sign in as: ssh deploy@<server-ip>"
