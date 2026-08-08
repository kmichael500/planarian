#!/usr/bin/env bash

# Load the small, portable dotenv subset used by Planarian deployment files
# without evaluating the file as shell code. Values may be unquoted or wrapped
# in matching single or double quotes; command substitution and other shell
# expressions remain literal text.
load_dotenv() {
  local dotenv_file="$1"
  local line key value line_number=0

  while IFS= read -r line || [[ -n "$line" ]]; do
    ((line_number += 1))
    line="${line%$'\r'}"

    [[ -z "${line//[[:space:]]/}" || "$line" =~ ^[[:space:]]*# ]] && continue
    line="${line#export }"
    if [[ ! "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
      echo "Invalid dotenv assignment at ${dotenv_file}:${line_number}. Use KEY=VALUE (optionally outer-quoted)." >&2
      return 1
    fi

    key="${BASH_REMATCH[1]}"
    value="${BASH_REMATCH[2]}"
    if [[ "$value" == \"* || "$value" == \'* ]]; then
      local quote="${value:0:1}"
      if [[ ${#value} -lt 2 || "${value: -1}" != "$quote" ]]; then
        echo "Unterminated quoted dotenv value at ${dotenv_file}:${line_number}." >&2
        return 1
      fi
      value="${value:1:${#value}-2}"
    fi

    printf -v "$key" '%s' "$value"
    export "$key"
  done < "$dotenv_file"
}
