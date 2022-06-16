# SimpleMailArchive

A simple web application to backup & archive email from an IMAP server on your own server in an .eml based file structure.

## How to use
Two options:
- [Docker container](https://hub.docker.com/r/axmeyer/simplemailarchive)
- Clone this repository to build and host the `SimpleMailArchiver.csproj` yourself

### Application Configuration
The application needs four paths as configuration, to be configured either in through mounted volumes in the docker container (see docker page or [`docker-compose.yml example`](example_docker-compose.yml) for reference) or in a [`config.json`](SimpleMailArchiver/SimpleMailArchiver/config.json_sample) file in the applications root folder.

The paths are:
- `ArchiveBasePath`: In this folder, the folder structure of your mail server is mirrored and every email is stored as an .eml file. 
- `ImportBasePath`: If you have an existing email archive of some kind, export the emails as .eml and place them in this folder. Then they can be imported through the web interface.
- `AccountConfigsPath`: A folder with `.account` config files to configure the email accounts to import from. Sample file [here](SimpleMailArchiver/SimpleMailArchiver/accounts/JohnDoe_gmail.account_sample)
- `DbPath`: The sqlite database for quick search in the achive will be stored here. Should be a fast drive (e.g. ssd) for good performance.

### Account configuration
An example for the account configuration can be found [here](SimpleMailArchiver/SimpleMailArchiver/accounts/JohnDoe_gmail.account_sample). At least one `.account` file needs to be placed in the `AccountConfigsPath`.

For explanation on what the options do exactly and what their default values are, see [`Account.cs`](SimpleMailArchiver/SimpleMailArchiver/Data/Account.cs) and [`FolderOptions.cs`](SimpleMailArchiver/SimpleMailArchiver/Data/FolderOptions.cs).

For the account configuration, required configuration values are `UserName`, `Password`, `ImapUrl` and `AccountDisplayName`, the rest is optional.

For a `FolderOption`, the `Name` field is required, and at least one other field should be set, otherwise the folder is still processed with the default values. For any folder without specific folder options, the default values are used.

### API
Import new e-mails from IMAP accounts can be triggered through an API call:

```your-domain/import-api?accountFilename=<account-file>&callBackUrl=<monitoring-url>```

Replace `<account-file>` with the configuration file for the account to be archived, e.g. `john_doe_gmail.account`.

The optional paramater `callBackUrl` allows integration with a service like [healthchecks.io](https://healthchecks.io).

If provided, the following `GET` queries are executed:
- `monitoring_url/start` at the beginning of the import
- `monitoring_url` at successfull completion
- `monitoring_url/fail` in case of failure

Sample for an API call:

```https://simplemailarchive.sampledomain.com/import-api?accountFilename=john_doe_gmail.account&callBackUrl=https://hc-ping.com/uuid-of-the-check```

