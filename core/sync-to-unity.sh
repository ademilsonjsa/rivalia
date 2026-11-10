#!/usr/bin/env bash
# Copia o núcleo de regras (fonte da verdade: core/Rivalia.Core) para o projeto Unity.
set -euo pipefail
cd "$(dirname "$0")"
dst=../game/Assets/_Rivalia/Scripts/Core
find "$dst" -name '*.cs' -delete
find Rivalia.Core -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -exec cp {} "$dst"/ \;
echo "Núcleo sincronizado em $dst"
