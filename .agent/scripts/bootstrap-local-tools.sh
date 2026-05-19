#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

install_dotnet_sdk_8() {
  if command -v dotnet >/dev/null 2>&1; then
    echo "dotnet already installed: $(dotnet --version)"
    return
  fi

  echo "Installing dotnet SDK 8..."
  tmp_dir="$(mktemp -d)"
  trap 'rm -rf "${tmp_dir}"' RETURN
  if ! curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${tmp_dir}/dotnet-install.sh"; then
    curl -fsSL https://aka.ms/dotnet-install.sh -o "${tmp_dir}/dotnet-install.sh"
  fi
  bash "${tmp_dir}/dotnet-install.sh" --channel 8.0
  export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:${PATH}"

  if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: dotnet installation failed." >&2
    exit 1
  fi

  echo "dotnet installed: $(dotnet --version)"
}

install_deno() {
  if command -v deno >/dev/null 2>&1; then
    echo "deno already installed: $(deno --version | head -n 1)"
    return
  fi

  echo "Installing deno..."
  curl -fsSL https://deno.land/install.sh | sh
  export PATH="${HOME}/.deno/bin:${PATH}"

  if ! command -v deno >/dev/null 2>&1; then
    echo "ERROR: deno installation failed." >&2
    exit 1
  fi

  echo "deno installed: $(deno --version | head -n 1)"
}

install_dotnet_sdk_8
install_deno

bash "${SCRIPT_DIR}/bootstrap-local-postgres.sh"

echo "Local bootstrap completed."
