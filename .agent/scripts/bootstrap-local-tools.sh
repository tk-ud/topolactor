#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ENV_SNIPPET="${HOME}/.topolactor-tools/env.sh"
DOTNET_BIN="${HOME}/.dotnet"
DOTNET_TOOLS_BIN="${HOME}/.dotnet/tools"
DENO_BIN="${HOME}/.deno/bin"

mkdir -p "$(dirname "${ENV_SNIPPET}")"

write_env_snippet() {
  cat > "${ENV_SNIPPET}" <<'SNIPPET'
#!/usr/bin/env bash
# topolactor local bootstrap tool PATH snippet
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.deno/bin:$PATH"
SNIPPET
  chmod +x "${ENV_SNIPPET}"
}

print_parent_shell_notice() {
  cat <<NOTICE

NOTE: This script runs in a child shell. PATH changes do not persist automatically to your current shell.
To use dotnet/deno in your current shell after bootstrap, run one of:

  source "${ENV_SNIPPET}"
  source ~/.profile   # if your profile sources ${ENV_SNIPPET}

Optional: add this line once to ~/.profile for persistent sessions:

  [ -f "${ENV_SNIPPET}" ] && source "${ENV_SNIPPET}"
NOTICE
}

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
  export PATH="${DOTNET_BIN}:${DOTNET_TOOLS_BIN}:${PATH}"

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
  export PATH="${DENO_BIN}:${PATH}"

  if ! command -v deno >/dev/null 2>&1; then
    echo "ERROR: deno installation failed." >&2
    exit 1
  fi

  echo "deno installed: $(deno --version | head -n 1)"
}

write_env_snippet
install_dotnet_sdk_8
install_deno

bash "${SCRIPT_DIR}/bootstrap-local-postgres.sh"
print_parent_shell_notice

echo "Local bootstrap completed."
