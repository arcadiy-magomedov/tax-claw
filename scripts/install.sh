#!/usr/bin/env sh
# Installs the tax-claw CLI (`taxclaw`) from a GitHub Release.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/arcadiy-magomedov/tax-claw/main/scripts/install.sh | sh
#
# Env overrides:
#   TAXCLAW_VERSION       install a specific tag (e.g. v0.1.0) instead of the latest release
#   TAXCLAW_INSTALL_DIR   where to symlink the `taxclaw` command (default: /usr/local/bin if
#                         writable, else ~/.local/bin)
#   TAXCLAW_LIB_DIR       where to unpack the actual binary + native libraries
#                         (default: ~/.local/lib/tax-claw)

set -eu

repo="arcadiy-magomedov/tax-claw"

os="$(uname -s)"
arch="$(uname -m)"

case "$os" in
  Darwin) platform="osx" ;;
  Linux) platform="linux" ;;
  *)
    echo "error: unsupported OS '$os'. tax-claw ships prebuilt binaries for macOS and Linux only." >&2
    echo "For Windows, download the win-x64/win-arm64 zip from:" >&2
    echo "  https://github.com/$repo/releases/latest" >&2
    exit 1
    ;;
esac

case "$arch" in
  arm64 | aarch64) cpu="arm64" ;;
  x86_64 | amd64) cpu="x64" ;;
  *)
    echo "error: unsupported architecture '$arch'." >&2
    exit 1
    ;;
esac

rid="${platform}-${cpu}"

version="${TAXCLAW_VERSION:-latest}"
if [ "$version" = "latest" ]; then
  api_url="https://api.github.com/repos/$repo/releases/latest"
else
  api_url="https://api.github.com/repos/$repo/releases/tags/$version"
fi

echo "Looking up the $version release for $rid..."
asset_url="$(
  curl -fsSL "$api_url" \
    | grep -o '"browser_download_url": *"[^"]*"' \
    | sed -E 's/.*"(https:[^"]+)"/\1/' \
    | grep "tax-claw-.*-${rid}\\.tar\\.gz\$" \
    | head -n1
)"

if [ -z "$asset_url" ]; then
  echo "error: could not find a release asset for platform '$rid'." >&2
  echo "Check https://github.com/$repo/releases for available downloads." >&2
  exit 1
fi

install_dir="${TAXCLAW_INSTALL_DIR:-}"
if [ -z "$install_dir" ]; then
  if [ -w /usr/local/bin ] 2>/dev/null; then
    install_dir=/usr/local/bin
  else
    install_dir="$HOME/.local/bin"
  fi
fi
mkdir -p "$install_dir"

lib_dir="${TAXCLAW_LIB_DIR:-$HOME/.local/lib/tax-claw}"

echo "Downloading:"
echo "  $asset_url"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
curl -fsSL "$asset_url" -o "$tmp/tax-claw.tar.gz"

echo "Installing to $lib_dir"
rm -rf "${lib_dir:?}"
mkdir -p "$lib_dir"
tar -xzf "$tmp/tax-claw.tar.gz" -C "$lib_dir"
chmod +x "$lib_dir/taxclaw"

ln -sf "$lib_dir/taxclaw" "$install_dir/taxclaw"

echo
echo "Installed: $install_dir/taxclaw -> $lib_dir/taxclaw"
case ":$PATH:" in
  *":$install_dir:"*) echo "Run 'taxclaw' to get started." ;;
  *)
    echo "NOTE: $install_dir is not on your PATH. Add this to your shell profile:"
    echo "  export PATH=\"$install_dir:\$PATH\""
    ;;
esac
