root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
bench_env=$(mise -C "$root" env -s bash) || return 1
eval "$bench_env"

stop_session() {
  local session=$1 data=$2 pids
  pids=$(pgrep -f -- "--dataPath $data --tracelog" || true)
  tmux kill-session -t "=$session" 2>/dev/null || true
  # A dying game outlives its command line: pgrep -f stops matching while threads still hold its ports.
  for pid in $pids; do
    while kill -0 "$pid" 2>/dev/null; do sleep 0.5; done
  done
}

log_lines() { { wc -l < "$1"; } 2>/dev/null || echo 0; }

wait_for_log() {
  local log=$1 after=$2 line=$3 deadline=$((SECONDS + $4))
  until tail -n +"$((after + 1))" "$log" 2>/dev/null | grep -qF -- "$line"; do
    [ $SECONDS -lt $deadline ] || return 1
    sleep 0.5
  done
}

stop_server() {
  local session=$1 data=$2 log=$2/Logs/server-main.log after
  if tmux has-session -t "=$session" 2>/dev/null; then
    after=$(log_lines "$log")
    tmux send-keys -t "=$session:" '/stop' Enter
    wait_for_log "$log" "$after" "Stopped the server!" 60 ||
      echo "The world did not finish saving within a minute; stopping the server anyway" >&2
  fi
  stop_session "$session" "$data"
}

wait_until_running() {
  local session=$1 log=$2/Logs/server-main.log started=$3 seconds=$4 deadline=$((SECONDS + $4))
  until [ "$log" -nt "$started" ] && grep -qF "Dedicated Server now running" "$log"; do
    tmux has-session -t "=$session" 2>/dev/null || { echo "The server exited; read $log" >&2; exit 1; }
    [ $SECONDS -lt $deadline ] || { echo "The server did not start within $seconds s; read $log" >&2; exit 1; }
    sleep 1
  done
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
