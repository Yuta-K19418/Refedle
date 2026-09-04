#!/bin/sh
# Installs the latest released `refedle` binary for the current OS/architecture.
# Intended for piped use:
#   curl -fsSL https://raw.githubusercontent.com/Yuta-K19418/Refedle/main/install.sh | sh

set -eu

REPO_URL="https://github.com/Yuta-K19418/Refedle"
LATEST_URL="${REPO_URL}/releases/latest"

HTTP_CLIENT=""
SHA_TOOL=""
tmpdir=""
tmp_install=""

# --- output ----------------------------------------------------------------

info() {
    printf '%s\n' "$*"
}

error() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

cleanup() {
    [ -z "$tmpdir" ] || rm -rf "$tmpdir"
    [ -z "$tmp_install" ] || rm -f "$tmp_install"
}

# --- dependency detection ------------------------------------------------

detect_http_client() {
    if command -v curl >/dev/null 2>&1; then
        HTTP_CLIENT="curl"
    elif command -v wget >/dev/null 2>&1; then
        HTTP_CLIENT="wget"
    else
        error "This installer needs 'curl' or 'wget', but neither is installed."
    fi
}

detect_sha_tool() {
    if command -v sha256sum >/dev/null 2>&1; then
        SHA_TOOL="sha256sum"
    elif command -v shasum >/dev/null 2>&1; then
        SHA_TOOL="shasum"
    else
        error "This installer needs 'sha256sum' or 'shasum' to verify the download, but neither is installed."
    fi
}

# --- HTTP --------------------------------------------------------------

# resolve_effective_url <url> : print the URL after following every redirect.
resolve_effective_url() {
    if [ "$HTTP_CLIENT" = "curl" ]; then
        curl -fsSL -o /dev/null -w '%{url_effective}' "$1"
        return
    fi
    wget -S --spider "$1" 2>&1 \
        | awk 'tolower($1) == "location:" { location = $2 } END { print location }' \
        | tr -d '\r'
}

# http_status <url> : print the final HTTP status code, or 000 if unreachable.
http_status() {
    if [ "$HTTP_CLIENT" = "curl" ]; then
        if hs_code=$(curl -sIL -o /dev/null -w '%{http_code}' "$1" 2>/dev/null); then
            printf '%s' "$hs_code"
        else
            printf '000'
        fi
        return
    fi
    wget -S --spider "$1" 2>&1 \
        | awk '$1 ~ /^HTTP\// { code = $2 } END { print (code == "" ? "000" : code) }'
}

# download <url> <dest> : download <url> to <dest>, failing on any HTTP error.
download() {
    if [ "$HTTP_CLIENT" = "curl" ]; then
        curl -fsSL -o "$2" "$1"
        return
    fi
    wget -q -O "$2" "$1"
}

# --- release resolution ----------------------------------------------------

get_latest_tag() {
    glt_url=$(resolve_effective_url "$LATEST_URL") \
        || error "Could not reach ${LATEST_URL} to determine the latest release."
    [ -n "$glt_url" ] || error "Could not determine the latest release from ${LATEST_URL}."
    case "$glt_url" in
        */releases/tag/*) : ;;
        *) error "No published release was found at ${LATEST_URL}." ;;
    esac
    glt_tag=${glt_url##*/}
    [ -n "$glt_tag" ] || error "Could not parse the release tag from ${glt_url}."
    printf '%s\n' "$glt_tag"
}

# --- platform ------------------------------------------------------------

resolve_rid() {
    rr_os=$(uname -s)
    rr_arch=$(uname -m)

    case "$rr_os" in
        Linux) rr_os_slug="linux" ;;
        Darwin) rr_os_slug="osx" ;;
        MINGW* | MSYS* | CYGWIN* | Windows_NT)
            error "Windows is not supported by this installer. Download the Windows .zip from ${REPO_URL}/releases and extract it manually." ;;
        *)
            error "Unsupported operating system '${rr_os}'. refedle ships binaries for Linux and macOS only." ;;
    esac

    case "$rr_arch" in
        x86_64 | amd64) rr_arch_slug="x64" ;;
        arm64 | aarch64) rr_arch_slug="arm64" ;;
        *)
            error "Unsupported architecture '${rr_arch}'. refedle ships x64 and arm64 binaries only." ;;
    esac

    if [ "$rr_os_slug" = "osx" ] && [ "$rr_arch_slug" = "x64" ]; then
        error "macOS on Intel is not supported; build from source: ${REPO_URL}"
    fi

    printf '%s-%s\n' "$rr_os_slug" "$rr_arch_slug"
}

# --- checksum ------------------------------------------------------------

sha256_hex() {
    case "$SHA_TOOL" in
        sha256sum) sha256sum "$1" | awk '{ print $1 }' ;;
        shasum) shasum -a 256 "$1" | awk '{ print $1 }' ;;
    esac
}

# verify_checksum <archive> <archive-name> <checksums-file>
verify_checksum() {
    vc_archive="$1"
    vc_name="$2"
    vc_sums="$3"

    vc_expected=$(awk -v name="$vc_name" '
        {
            hash = $1
            file = $2
            sub(/^[*]/, "", file)
            if (file == name && length(hash) == 64) {
                print tolower(hash)
                found = 1
                exit
            }
        }
        END { if (!found) exit 1 }
    ' "$vc_sums") || error "checksums.txt has no entry for ${vc_name}."

    vc_actual=$(sha256_hex "$vc_archive" | tr 'A-F' 'a-f')

    [ "$vc_expected" = "$vc_actual" ] \
        || error "Checksum mismatch for ${vc_name} (expected ${vc_expected}, got ${vc_actual})."
}

# --- install location --------------------------------------------------

select_install_dir() {
    set --
    [ -z "${XDG_BIN_HOME:-}" ] || set -- "$@" "$XDG_BIN_HOME"
    [ -z "${HOME:-}" ] || set -- "$@" "${HOME}/.local/bin"
    set -- "$@" "/usr/local/bin"

    for sid_dir in "$@"; do
        if [ -d "$sid_dir" ] && [ -w "$sid_dir" ]; then
            printf '%s\n' "$sid_dir"
            return 0
        fi
    done

    for sid_dir in "$@"; do
        if mkdir -p "$sid_dir" 2>/dev/null && [ -w "$sid_dir" ]; then
            printf '%s\n' "$sid_dir"
            return 0
        fi
    done

    error "No writable install directory was found (tried \$XDG_BIN_HOME, \$HOME/.local/bin, /usr/local/bin). Set XDG_BIN_HOME to a writable directory and re-run."
}

# --- success message ---------------------------------------------------

print_path_hint() {
    pph_dir="$1"

    case ":${PATH:-}:" in
        *":${pph_dir}:"*)
            return 0
            ;;
    esac

    info ""
    info "${pph_dir} is not on your PATH. Add it, then restart your shell:"
    pph_shell=""
    [ -z "${SHELL:-}" ] || pph_shell=$(basename "$SHELL")
    case "$pph_shell" in
        bash) info "  echo 'export PATH=\"${pph_dir}:\$PATH\"' >> ~/.bashrc" ;;
        zsh) info "  echo 'export PATH=\"${pph_dir}:\$PATH\"' >> ~/.zshrc" ;;
        fish) info "  fish_add_path ${pph_dir}" ;;
        *) info "  export PATH=\"${pph_dir}:\$PATH\"" ;;
    esac
}

# --- main --------------------------------------------------------------

main() {
    detect_http_client
    detect_sha_tool

    info "Resolving the latest refedle release..."
    tag=$(get_latest_tag)
    rid=$(resolve_rid)
    info "Latest release: ${tag} (${rid})"

    archive_name="refedle-${tag}-${rid}.tar.gz"
    archive_url="${REPO_URL}/releases/download/${tag}/${archive_name}"
    checksums_url="${REPO_URL}/releases/download/${tag}/checksums.txt"

    tmpdir=$(mktemp -d 2>/dev/null || mktemp -d -t refedle-install) \
        || error "Could not create a temporary directory."
    trap cleanup EXIT

    info "Downloading checksums.txt..."
    if ! download "$checksums_url" "${tmpdir}/checksums.txt" 2>/dev/null; then
        cs_status=$(http_status "$checksums_url")
        case "$cs_status" in
            404) error "Release ${tag} does not provide checksums.txt, so the download cannot be verified. Aborting." ;;
            *) error "Failed to download checksums.txt from ${checksums_url} (HTTP ${cs_status})." ;;
        esac
    fi

    ar_status=$(http_status "$archive_url")
    case "$ar_status" in
        200) : ;;
        404) error "Release asset not found: ${archive_name}. The ${rid} build may not be published for ${tag}." ;;
        000) error "Could not reach GitHub to download ${archive_name}." ;;
        *) error "Unexpected HTTP status ${ar_status} for ${archive_name}." ;;
    esac

    info "Downloading ${archive_name}..."
    download "$archive_url" "${tmpdir}/${archive_name}" \
        || error "Failed to download ${archive_name} from ${archive_url}."

    info "Verifying checksum..."
    verify_checksum "${tmpdir}/${archive_name}" "$archive_name" "${tmpdir}/checksums.txt"

    mkdir -p "${tmpdir}/extract"
    tar -xzf "${tmpdir}/${archive_name}" -C "${tmpdir}/extract" \
        || error "Could not extract ${archive_name}."
    binary_path=$(find "${tmpdir}/extract" -type f -name refedle -print 2>/dev/null | head -n 1)
    [ -n "$binary_path" ] || error "The archive ${archive_name} did not contain a 'refedle' binary."

    install_dir=$(select_install_dir)
    info "Installing to ${install_dir}/refedle..."
    tmp_install=$(mktemp "${install_dir}/.refedle.install.XXXXXX") \
        || error "Could not create a staging file in ${install_dir}."
    cp "$binary_path" "$tmp_install"
    chmod 755 "$tmp_install"
    mv -f "$tmp_install" "${install_dir}/refedle"
    tmp_install=""

    info ""
    info "refedle ${tag} installed to ${install_dir}/refedle"
    print_path_hint "$install_dir"
    info ""
    info "Run 'refedle version' to verify the installation."
}

main "$@"
