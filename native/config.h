#ifndef BING_WALLPAPER_CONFIG_H
#define BING_WALLPAPER_CONFIG_H

// The tray reads only two fields from the flat UTF-8 configuration object.
static const char *skipSpace(const char *p)
{
    while (*p == ' ' || *p == '\r' || *p == '\n' || *p == '\t') ++p;
    return p;
}

static int hexDigit(char c)
{
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    return -1;
}

static const char *readString(const char *p, wchar_t *out, size_t cap)
{
    size_t n = 0;
    if (*p != '"') return NULL;
    ++p;
    while (*p && *p != '"') {
        wchar_t ch;
        if (*p == '\\') {
            if (!*++p) return NULL;
            switch (*p) {
            case 'u':
                ch = 0; ++p;
                for (int i = 0; i < 4; ++i) {
                    int digit = hexDigit(*p);
                    if (digit < 0) return NULL;
                    ch = (wchar_t)(ch * 16 + digit); ++p;
                }
                break;
            case 'n': ch = '\n'; ++p; break;
            case 'r': ch = '\r'; ++p; break;
            case 't': ch = '\t'; ++p; break;
            case 'b': ch = '\b'; ++p; break;
            case 'f': ch = '\f'; ++p; break;
            case '"': case '\\': case '/': ch = *p++; break;
            default: return NULL;
            }
        } else if ((unsigned char)*p >= 128) {
            unsigned char first = (unsigned char)*p;
            int len = first < 224 ? 2 : first < 240 ? 3 : 4;
            for (int i = 1; i < len; ++i) if (!p[i]) return NULL;
            wchar_t converted[2];
            int count = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, p, len, converted, 2);
            if (!count) return NULL;
            for (int i = 0; i < count; ++i) if (n + 1 < cap) out[n++] = converted[i];
            p += len;
            continue;
        } else {
            if ((unsigned char)*p < 32) return NULL;
            ch = (unsigned char)*p++;
        }
        if (n + 1 < cap) out[n++] = ch;
    }
    if (*p != '"') return NULL;
    if (cap) out[n] = 0;
    return p + 1;
}

static char *readFile(const wchar_t *path)
{
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                              NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) return NULL;
    DWORD size = GetFileSize(file, NULL), read = 0;
    if (size > 1024 * 1024) { CloseHandle(file); return NULL; }
    char *data = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, (size_t)size + 1);
    if (data && !ReadFile(file, data, size, &read, NULL)) {
        HeapFree(GetProcessHeap(), 0, data);
        data = NULL;
    }
    CloseHandle(file);
    return data;
}

static int jsonValue(const char *json, const wchar_t *key, wchar_t *out, size_t cap)
{
    if (!json || !cap) return 0;
    if ((unsigned char)json[0] == 0xef && (unsigned char)json[1] == 0xbb && (unsigned char)json[2] == 0xbf) json += 3;
    const char *p = skipSpace(json);
    if (*p != '{') return 0;
    ++p;
    while (*(p = skipSpace(p)) && *p != '}') {
        wchar_t name[80];
        p = readString(p, name, ARRAYSIZE(name));
        if (!p) return 0;
        p = skipSpace(p);
        if (*p != ':') return 0;
        p = skipSpace(p + 1);
        int match = wcscmp(name, key) == 0;
        if (*p == '"') {
            // Skip unrelated values without allocating another 64 KiB scratch buffer.
            p = readString(p, match ? out : NULL, match ? cap : 0);
            if (!p) return 0;
        } else {
            size_t n = 0;
            while (*p && *p != ',' && *p != '}') {
                if (match && n + 1 < cap) out[n++] = (unsigned char)*p;
                ++p;
            }
            while (n && (out[n - 1] == ' ' || out[n - 1] == '\r' || out[n - 1] == '\n' || out[n - 1] == '\t')) --n;
            if (match) out[n] = 0;
        }
        if (match) return 1;
        p = skipSpace(p);
        if (*p == ',') ++p;
        else if (*p != '}') return 0;
    }
    return 0;
}

static int parseDailyTime(const wchar_t *value, int *hour, int *minute)
{
    if (wcslen(value) != 5 || value[2] != ':') return 0;
    const int positions[] = { 0, 1, 3, 4 };
    for (int i = 0; i < 4; ++i) if (value[positions[i]] < '0' || value[positions[i]] > '9') return 0;
    int h = (value[0] - '0') * 10 + value[1] - '0';
    int m = (value[3] - '0') * 10 + value[4] - '0';
    if (h >= 24 || m >= 60) return 0;
    *hour = h; *minute = m;
    return 1;
}
#endif
