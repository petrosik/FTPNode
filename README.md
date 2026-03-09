# Overview

A configurable web-based FTP client built with Blazor. It provides a browser interface for managing files on FTP servers with a modular architecture that allows extensive configuration and customization of features, behaviors, and integrations.

**Do not forget setup network info for the container** (depending on use case it might need network connection to internet)

# Config Overview

This application uses a layered configuration system to allow maximum flexibility. Configuration values can come from:

1. Default `appsettings.json` baked into the container.
2. Optional `config/appsettings.json` that will override the default config.
3. Environment variables (highest precedence).

---

## Using an Optional Config Folder

You can optionally mount a host folder containing configuration overrides. The container will use any files in this folder to override the defaults.

Example:

```bash
docker run -p 8080:80
  -v /host/path/config:/app/config
  my-ftp-web-view
```

- `/host/path/config` is a folder on the host machine.
- The container will look for `/app/config/appsettings.json`.
- If the folder is not mounted, the container will simply use the defaults baked in.

---

## Overriding with Environment Variables

You can override any configuration value using environment variables.

Example:

```bash
docker run -p 8080:80
  -e FTP__Title="FTP Custom Title"
  -e FTP__Port="21"
  my-ftp-web-view
```

- Note: Use double underscores `__` to replace `:` in JSON paths.
- Environment variables override any value in both default and override JSON files.

---

## Settings

You can override default values by editing: `config/appsettigns.json`  
Only include the settings you want to change. Any omitted values will use the built-in defaults.

Example:

```bash
 "FTP": {
   "Title": "FTP Custom Title",
   "Host": "172.0.0.1",
   "Port": 21
 }
```

**Important**
  - Settings must be placed inside the `"FTP"` section.
  - All variables can be passed in as string

### Variables

- **title**
  - Type: `string`
  
- **host**
  - Type: `string`
  
- **port**
  - Type: `number`
  
- **passivemode**
  - Type: `bool`
  
- **validateanycertificate**
  - Type: `bool`
  - Default: false
  - Description: When true automaticly accepts any ftp connection certificate, otherwise asks user
  
- **downloadlimit**
  - Type: `number` (in bytes)
  - Default: 131072 (128KB)

- **uploadlimit**
  - Type: `number` (in bytes)
  - Default: 65536 (64KB)

- **maxfileuploadsize**
  - Type: `number` (in bytes)
  - Default: 536870912000 (500GB)
  - Description: Maximum file size allowed by the frontend

- **autodelete**
  - Type: `bool`
  - Default: true
  - Description: Determines if incomplete files should be deleted from the folder when connection or error occurs while uploading
  
- **simultaneousupdown**
  - Type: `bool`
  - Default: false
  - Description: Changes if upload and download can run at the same time

- **maxeditsize**
  - Type: `number` (in bytes)
  - Default: 1048576 (1MB)
  - Description: Determines file size the frontend is allowed to edit or view
  
- **disablepermchange**
  - Type: `bool`
  - Default: false
  - Description: Determines if frontend shows permission change button