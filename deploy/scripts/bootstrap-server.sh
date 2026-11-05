#!/bin/sh
# One-time preparation of a fresh Ubuntu 24.04 server (e.g. Oracle Cloud Always Free, Ampere ARM64):
#   sudo sh deploy/scripts/bootstrap-server.sh
# Opens HTTP/HTTPS in the host firewall, installs Docker Engine with the Compose plugin from Docker's
# repository, enables unattended security updates and creates the backup directory. Safe to run again.
set -eu
[ "$(id -u)" -eq 0 ] || { echo "Run with sudo: sudo sh $0" >&2; exit 1; }
user=${SUDO_USER:-ubuntu}

echo "== Firewall: allow 80/tcp, 443/tcp, 443/udp (Oracle images reject them by default)"
for rule in "tcp 80" "tcp 443" "udp 443"; do
  set -- $rule
  if ! iptables -C INPUT -m state --state NEW -p "$1" --dport "$2" -j ACCEPT 2>/dev/null; then
    # Insert before the image's final REJECT rule, or append when there is none.
    reject=$(iptables -L INPUT --line-numbers -n | awk '$2 == "REJECT" { print $1; exit }')
    if [ -n "$reject" ]; then iptables -I INPUT "$reject" -m state --state NEW -p "$1" --dport "$2" -j ACCEPT
    else iptables -A INPUT -m state --state NEW -p "$1" --dport "$2" -j ACCEPT; fi
  fi
done
command -v netfilter-persistent >/dev/null && netfilter-persistent save

echo "== Docker Engine and Compose plugin"
apt-get update -q
apt-get install -y -q ca-certificates curl unattended-upgrades git
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
. /etc/os-release
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $VERSION_CODENAME stable" \
  > /etc/apt/sources.list.d/docker.list
apt-get update -q
apt-get install -y -q docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
usermod -aG docker "$user"

echo "== Automatic security updates"
dpkg-reconfigure -f noninteractive unattended-upgrades

echo "== Backup directory (owned by the containers' user, UID 1654)"
mkdir -p /srv/medical-backups
chown 1654:1654 /srv/medical-backups

echo
echo "Done. Log out and back in (exit, then ssh again) so '$user' can use docker without sudo."
docker --version
docker compose version
