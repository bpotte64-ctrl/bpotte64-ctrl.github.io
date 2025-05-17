set -euo pipefail

FRAMEWORK="$1"

# shellcheck disable=SC2116
WASM=$(echo "$FRAMEWORK"/dotnet.native.*.wasm)
BLAZORBOOT="$FRAMEWORK/blazor.boot.json"

wasm2wat --enable-all "$WASM" > wasm.wat

(
	cd patch || exit 1
	dotnet run -c Release ../wasm.wat ../wasmreplaced.wat
)

wat2wasm --enable-all wasmreplaced.wat -o "$WASM"
rm wasm.wat wasmreplaced.wat
jq --arg a "sha256-$(openssl dgst -sha256 -binary "$WASM" | openssl base64 -A)" '.resources.wasmNative[.resources.wasmNative | keys[0]] = $a' "$BLAZORBOOT" > blazor.boot.json
mv blazor.boot.json "$BLAZORBOOT"
