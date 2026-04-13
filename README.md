# Telltale 🎋

A bilingual children's story generator with AI-illustrated pages and narration. Enter a few keywords, and Telltale writes a full illustrated story in both English and Chinese — complete with cartoon oil-painting illustrations and voice narration.

**Frontend repo:** [MQLite/TelltaleClient](https://github.com/MQLite/TelltaleClient)

[![Built with Pollinations.AI](https://img.shields.io/badge/Built%20with-Pollinations.AI-ff6a00?style=flat&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0id2hpdGUiIGQ9Ik0xMiAyQzYuNDggMiAyIDYuNDggMiAxMnM0LjQ4IDEwIDEwIDEwIDEwLTQuNDggMTAtMTBTMTcuNTIgMiAxMiAyem0tMSAxNy45M1Y0LjA3YzMuOTMuNDkgNyAzLjg1IDcgNy45M3MtMy4wNyA3LjQ0LTcgNy45M3oiLz48L3N2Zz4=)](https://pollinations.ai)

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

---

## Getting Started (Development)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- Pollinations.ai API key — register free at [auth.pollinations.ai](https://auth.pollinations.ai)

### Backend

```bash
git clone https://github.com/MQLite/Telltale.git
cd Telltale

# Set API key via User Secrets
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

The Vite dev server proxies `/api/*` to `http://localhost:5000` automatically — no CORS configuration needed in development.

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

## Production Deployment

Both backend and frontend are built as static artifacts first, then served by a reverse proxy.

### Build

```bash
# Backend — publish self-contained release
cd Telltale
dotnet publish -c Release -o ./publish

# Frontend — build static files
cd TelltaleClient
npm run build
# Output in ./dist/
```

---

### Ubuntu 22

#### 1. Install dependencies

```bash
# .NET 8 runtime
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-runtime-8.0

# nginx
sudo apt install -y nginx
```

#### 2. Deploy files

```bash
sudo mkdir -p /var/www/telltale/{api,web,data}
sudo useradd -r -s /bin/false telltale
sudo chown -R telltale:telltale /var/www/telltale

# Upload build artifacts
rsync -av Telltale/publish/   user@server:/var/www/telltale/api/
rsync -av TelltaleClient/dist/ user@server:/var/www/telltale/web/
```

#### 3. API key secrets

```bash
sudo mkdir -p /etc/telltale
sudo nano /etc/telltale/secrets.env
```

```
Pollinations__ApiKey=your-key-here
```

```bash
sudo chown root:telltale /etc/telltale/secrets.env
sudo chmod 640 /etc/telltale/secrets.env
```

#### 4. systemd service

```bash
sudo nano /etc/systemd/system/telltale.service
```

```ini
[Unit]
Description=Telltale Story API
After=network.target

[Service]
Type=simple
User=telltale
WorkingDirectory=/var/www/telltale/api
ExecStart=/usr/bin/dotnet /var/www/telltale/api/Telltale.dll
Restart=on-failure
EnvironmentFile=/etc/telltale/secrets.env
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=Storage__Path=/var/www/telltale/data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now telltale
```

#### 5. nginx

```bash
sudo nano /etc/nginx/sites-available/telltale
```

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
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_read_timeout 120s;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/telltale /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

---

### Windows Server

#### 1. Install dependencies

- [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download) (includes ASP.NET Core runtime + IIS integration)
- [IIS](https://learn.microsoft.com/iis) — enable via *Server Manager → Add Roles → Web Server (IIS)*
- [URL Rewrite Module for IIS](https://www.iis.net/downloads/microsoft/url-rewrite)

#### 2. Deploy files

```
C:\inetpub\telltale\
  api\        ← dotnet publish output
  web\        ← npm run build output (dist/)
  data\       ← created automatically at runtime
```

#### 3. API key — environment variable

Set via *System Properties → Environment Variables* or PowerShell (run as Administrator):

```powershell
[System.Environment]::SetEnvironmentVariable("Pollinations__ApiKey", "your-key-here", "Machine")
```

#### 4. Backend — Windows Service

```powershell
# Install as a Windows Service (runs as NetworkService or a dedicated account)
sc.exe create Telltale `
  binPath= "dotnet C:\inetpub\telltale\api\Telltale.dll" `
  start= auto

sc.exe start Telltale
```

Or use IIS in-process hosting by adding a `web.config` alongside the published DLL:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*"
           modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet"
                arguments="Telltale.dll"
                stdoutLogEnabled="true"
                stdoutLogFile=".\logs\stdout"
                hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        <environmentVariable name="ASPNETCORE_URLS" value="http://127.0.0.1:5000" />
        <environmentVariable name="Storage__Path" value="C:\inetpub\telltale\data" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

#### 5. Frontend + reverse proxy — IIS

Create two IIS sites:

**Frontend site** (`telltale-web`) — serves `C:\inetpub\telltale\web\`  
Add a URL Rewrite rule to support SPA routing (send all non-file requests to `index.html`):

```xml
<rewrite>
  <rules>
    <rule name="SPA fallback" stopProcessing="true">
      <match url=".*" />
      <conditions logicalGrouping="MatchAll">
        <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
        <add input="{REQUEST_URI}" pattern="^/api" negate="true" />
      </conditions>
      <action type="Rewrite" url="/index.html" />
    </rule>
    <rule name="API proxy" stopProcessing="true">
      <match url="^api/(.*)" />
      <action type="Rewrite" url="http://127.0.0.1:5000/api/{R:1}"
              appendQueryString="true" />
    </rule>
  </rules>
</rewrite>
```

> **Note:** IIS reverse proxy requires the [Application Request Routing (ARR)](https://www.iis.net/downloads/microsoft/application-request-routing) module. Enable proxy in ARR settings after installing.

---

## Credits

AI features (story generation, image generation, and voice narration) are powered by [Pollinations.AI](https://pollinations.ai) — a free, open generative AI platform.

[![Pollinations.AI](https://image.pollinations.ai/prompt/pollinations%20logo%20white%20minimal?width=200&height=60&model=flux&nologo=true)](https://pollinations.ai)

---

## License

MIT
