# Version History

## v1.2.0
- **Feature**: EXIF metadata tagging — compressed files are now tagged with "Compressed by PhotoC" in the EXIF Software field. Files with this tag are automatically skipped on future runs. Filenames are no longer modified.
- **Bugfix**: Removed bounded queue limit (was 100) — all files are now queued regardless of folder size.
- **Bugfix**: Temp files are now written to the system Temp folder instead of the watched folder, fixing "Access denied" errors on phone-connected storage.
- **Bugfix**: Removed "Clear View" button from the log window that was causing crashes.

## v1.1.0
- **Feature**: Enabled recursive folder watching. PhotoC now watches and compresses images inside all nested sub-folders within the selected watched folder.
- **UI**: Enabled smooth (pixel-based) scrolling in the settings window.
- **Bugfix**: Fixed an issue where the "Run on startup" option was hidden.

## v1.0.0
- Initial release.
