## Settings
- **Format:** `name:value`
- **Notes:**
  - If the value contains spaces, enclose the entire value in double quotes `" "`
  - For multiple parameters, separate them using `|`
  - All of variables are optional and have default value if not set

### Variables

- **title**
  - Type: `string`
  - Note: this is what it should look like if there are spaces:`"title:My Web FTP"` otherwise `title:MyFTP`

- **defaultconnection**
  - Type: `string|int|bool`
  - Example: `10.0.0.1|21|true`
  - Description: All segments are optional. Valid formats include `hostname`, `hostname|port`, or `hostname|port|passivemode`.

- **downloadlimit**
  - Type: `number` (in bytes)
  - Default: 524288 (512KB)

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
  - Changes if upload and download can run at the same time
