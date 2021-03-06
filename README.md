# Satisfactory Backup/Snapshot Tool
Create backups/snapshots of a Satisfactory installation to manage different versions.

![App screenshot](Images/SatisfactorySnapshotScreen01.png?raw=true "Main screen")

For a working preview just download latest available `SatisfactorySnapshotTool-bin.zip` from [here](https://github.com/mibbio/SatisfactorySnapshotTool/releases)

## Features
* create backups of game files and savegames
	* file deduplication
		* uses [hard links](https://en.wikipedia.org/wiki/Hard_link) if a file already exists in another backup
	* every backup has its own savegames
	* launch game directly from an backup
		* temporarily swaps "live" savegames with savegames from backup
		* updates savegames in backup and restores "live" savegames after game exit

### Hints
* Backups of Early Access & Experimental version can exist side by side
* It's recommended to disable cloud saves for the game (both versions) as it may interfere with saves from backups
* **No game crack or any other manipulation to game files is used.** It just automates the method shown in an [official Coffee Stain video](https://youtu.be/Fsod_UF5eus?t=190)
