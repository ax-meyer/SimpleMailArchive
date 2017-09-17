# pyMailArchive
Eml Based Mail Archival Tool written in Python

This tool fetches E-Mails via IMAP and archives them in the folder structure of the server on the harddrive. Each E-Mail is saved in an individual .eml file.
It also creates sqlite database containing the metadata & savepath of each mail.
Archived e-mails can thus be browsed and filtered comfotably with the build-in viewer or an sqlite viewer of your choice.

# Requirements
Following pyhton libraries are required:

 sys
 
 os
 
 os
 
 sys
 
 re
 
 subprocess
 
 imaplib
 
 dateutil.parser
 
 hashlib
 
 json
 
 email
 
 shutil
 
 time

The tool has been tested to work fine on Windows and Ubuntu, running Python 3.x and Qt4 or Qt5. Other versions of Python should work finde as well.

# Getting Started

1) Clone the repository
2) Create pyMailArchiveConfig.ini from pyMailArchiveConfig.ini_example - minimal requirement: Set a BasePath for the location of the *eml archive
3) For every mail account, create a .account file from the .account_example file.
4) Start the programm by running "python mailArchiverQt.py"

# Disclaimer
I have been using the programm in its current form for a couple of months now without any issues.
That said, it is still not production-level software. Use it at your own risks!
