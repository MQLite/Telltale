# Telltale 🎋

A bilingual children's story generator with AI-illustrated pages and narration. Enter a few keywords, and Telltale writes a full illustrated story in both English and Chinese — complete with cartoon oil-painting illustrations and voice narration.

**Frontend repo:** [MQLite/TelltaleClient](https://github.com/MQLite/TelltaleClient)

---

## Features

- **Bilingual stories** — every page rendered in both English and Chinese
- **AI illustrations** — cartoon oil-painting style images via Pollinations.ai (Flux model)
- **Voice narration** — six voice options (Fable, Nova, Shimmer, Alloy, Echo, Onyx) powered by Pollinations TTS
- **Story persistence** — stories and images cached to disk; reopen saved stories instantly
- **Two-level cache** — memory + disk cache for both stories and images (no repeated API calls)
- **Canvas paint animation** — illustrations revealed with a brush-stroke painting effect

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8 Minimal API |
| Story generation | Pollinations.ai `/v1/chat/completions` (nova-fast) |
| Image generation | Pollinations.ai image API (Flux model) |
| TTS narration | Pollinations.ai audio API |
| Frontend | React + TypeScript (Vite) |
| Caching | IMemoryCache + file system (SHA256 hash filenames) |
| Deployment | systemd + nginx on Ubuntu 22 |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- Pollinations.ai API key — register free at [auth.pollinations.ai](https://auth.pollinations.ai)

### Backend

```bash
git clone https://github.com/MQLite/Telltale.git
cd Telltale

# Set API key via User Secrets (development)
dotnet user-secrets set "Pollinations:ApiKey" "your-key-here"

dotnet run
# Listening on http://localhost:5000
```

### Frontend

```bash
git clone https://github.com/MQLite/TelltaleClient.git
cd TelltaleClient
npm install
npm run dev
# Open http://localhost:5173
```

The Vite dev server proxies `/api/*` to `http://localhost:5000` automatically.

---

## Configuration

`appsettings.json` — all sensitive values should be set via User Secrets (dev) or environment variables (prod):

```json
{
  "Pollinations": {
    "ApiKey": "",
    "Model": "flux",
    "TextModel": "nova-fast",
    "Width": 800,
    "Height": 520,
    "TtsVoiceEn": "fable",
    "TtsVoiceZh": "nova"
  },
  "Cache": {
    "StoryTtlHours": 24,
    "ImageTtlHours": 24
  },
  "Storage": {
    "Path": "./data"
  }
}
```

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/story/generate` | Generate a bilingual story |
| `GET` | `/api/story/list` | List persisted stories |
| `GET` | `/api/image` | Proxy + cache image from Pollinations |
| `GET` | `/api/tts` | Single-page TTS audio |
| `POST` | `/api/tts/batch` | Batch TTS for all pages |

---

## Production Deployment (Ubuntu 22)

### systemd service

```ini
[Service]
User=telltale
WorkingDirectory=/var/www/telltale/api
ExecStart=/usr/bin/dotnet /var/www/telltale/api/Telltale.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=Storage__Path=/var/www/telltale/data
EnvironmentFile=/etc/telltale/secrets.env
```

`/etc/telltale/secrets.env` (chmod 640):
```
Pollinations__ApiKey=your-key-here
```

### nginx

```nginx
server {
    listen 80;
    server_name your-domain.com;
    root /var/www/telltale/web;
    index index.html;

    location /api/ {
        proxy_pass         http://127.0.0.1:5000/api/;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_read_timeout 120s;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

---

## License

MIT
