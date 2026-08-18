#!/bin/zsh
set -u

children=()
cleanup() {
  for child in "${children[@]:-}"; do
    kill "$child" 2>/dev/null || true
    wait "$child" 2>/dev/null || true
  done
}
trap cleanup EXIT
trap 'exit 0' INT TERM

while true; do
  interface=$(/sbin/route -n get default 2>/dev/null | awk '/interface:/{print $2; exit}')
  lan_ip=$(/usr/sbin/ipconfig getifaddr "$interface" 2>/dev/null || true)
  if [[ -z "$lan_ip" ]]; then
    sleep 2
    continue
  fi

  /usr/bin/dns-sd -P Planarian _https._tcp local 443 planarian.local "$lan_ip" &
  children=($!)
  /usr/bin/dns-sd -P PlanarianViewer _https._tcp local 443 viewer.planarian.local "$lan_ip" &
  children+=($!)
  /usr/bin/dns-sd -P PlanarianCert _http._tcp local 80 cert.planarian.local "$lan_ip" &
  children+=($!)

  while true; do
    sleep 10
    current=$(/usr/sbin/ipconfig getifaddr "$interface" 2>/dev/null || true)
    if [[ "$current" != "$lan_ip" ]]; then
      cleanup
      children=()
      break
    fi
  done
done
