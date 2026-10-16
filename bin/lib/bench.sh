root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
eval "$(mise -C "$root" env -s bash)"

stop_session() {
  local session=$1 data=$2
  tmux kill-session -t "$session" 2>/dev/null || true
  while pgrep -f -- "--dataPath $data --tracelog" >/dev/null; do sleep 0.5; done
}

require_free_port() {
  local port=$1 what=$2
  if ss -ltnH "sport = :$port" | grep -q .; then
    echo "Port $port ($what) is taken, most likely by another clone's bench." >&2
    echo "Give this clone its own ports in $root/mise.local.toml; bin/game help shows how." >&2
    exit 1
  fi
}

game_env() {
  mkdir -p "$BENCH_TMP"
  local dotnet
  dotnet=$(dirname "$(command -v dotnet)")
  echo "PATH=$dotnet:\$PATH DOTNET_ROOT=$dotnet TMPDIR=$BENCH_TMP AGENT_BRIDGE_PORT=$BENCH_BRIDGE_PORT"
}
