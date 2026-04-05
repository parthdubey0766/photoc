# PhotoC Test Cases

This document lists practical test cases to validate PhotoC functionality and prevent regressions.

## 1. Test Environment

- OS: Windows 10/11 (x64)
- .NET SDK: 10.0+
- Build mode: Debug and Release
- Test assets folder with sample images:
  - Large JPEG (10 MB+)
  - Small JPEG (< 2 MB)
  - PNG
  - WebP
  - BMP
  - TIFF
  - Corrupt image file (bad bytes)

## 2. Smoke Tests

### TC-SM-01: Build succeeds
- Steps:
  1. Run: dotnet build PhotoC.csproj -c Debug
- Expected:
  - Build succeeds with 0 errors.

### TC-SM-02: App starts without crash
- Steps:
  1. Run: dotnet run --project PhotoC.csproj -c Debug
  2. Wait 10 seconds.
- Expected:
  - Process stays alive.
  - No XamlParseException in terminal.
  - Tray icon appears.

### TC-SM-03: First run setup flow
- Steps:
  1. Ensure WatchedFolderPath is empty in %APPDATA%\\PhotoC\\appsettings.json.
  2. Start app.
- Expected:
  - Settings window opens.
  - App does not exit unexpectedly.

## 3. Regression Tests (Known Failures)

### TC-RG-01: MaterialDesign resource path regression
- Steps:
  1. Start app from clean build.
- Expected:
  - App loads resources successfully.
  - No error: Cannot locate resource themes/materialdesigntheme.defaults.xaml.

### TC-RG-02: Tray notification startup regression
- Steps:
  1. Start app with no watched folder configured.
  2. Observe startup path that triggers notification.
- Expected:
  - No crash with InvalidOperationException: TrayIcon is not created.
  - If notification cannot be shown, app continues running and logs a warning.

## 4. Functional Tests

### TC-FN-01: Save settings
- Steps:
  1. Open Settings.
  2. Set folder path and quality values.
  3. Click Save & Apply.
  4. Restart app.
- Expected:
  - Values persist in %APPDATA%\\PhotoC\\appsettings.json.
  - UI reloads saved values.

### TC-FN-02: Watcher starts on valid folder
- Steps:
  1. Set an existing watched folder.
  2. Save settings.
- Expected:
  - Status changes to Watching Active.
  - Log contains watching started message.

### TC-FN-03: Debounce behavior
- Steps:
  1. Copy a large JPEG into watched folder.
  2. Trigger multiple updates on same file within 5 seconds.
- Expected:
  - File is queued once after debounce period.
  - No partial/corrupt processing.

### TC-FN-03A: Existing files processed on startup
- Steps:
  1. Place supported images in watched folder before launching app.
  2. Start app.
- Expected:
  - Existing supported files are queued automatically.
  - Log includes "Queued <N> existing files from watched folder.".

### TC-FN-04: JPEG compression
- Steps:
  1. Add large JPEG to watched folder.
- Expected:
  - Output file is smaller than input.
  - EXIF metadata remains present.

### TC-FN-05: PNG compression
- Steps:
  1. Add PNG file.
- Expected:
  - Output remains PNG.
  - File is equal or smaller.
  - Visual content unchanged.

### TC-FN-06: WebP compression
- Steps:
  1. Add WebP file.
- Expected:
  - Output remains WebP.
  - File size reduced or skip logged if already optimal.

### TC-FN-07: BMP/TIFF conversion to PNG
- Steps:
  1. Add BMP and TIFF files.
- Expected:
  - Output files are PNG.
  - Original BMP/TIFF replaced only after successful temp output.

### TC-FN-08: Skip threshold
- Steps:
  1. Set MinFileSizeKB to 2048.
  2. Add a file below threshold.
- Expected:
  - File is skipped.
  - Skip reason logged.

### TC-FN-09: Pause/Resume monitoring
- Steps:
  1. Use tray menu Pause Monitoring.
  2. Drop new image.
  3. Resume monitoring.
- Expected:
  - While paused, image is not processed.
  - After resume, new files are processed.

### TC-FN-10: Single instance behavior
- Steps:
  1. Start app once.
  2. Start app second time.
- Expected:
  - Second launch shows already running message and exits.

### TC-FN-11: Run on startup registry setting
- Steps:
  1. Enable Run on startup in Settings and save.
  2. Verify HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run contains PhotoC.
  3. Disable and verify removal.
- Expected:
  - Registry value added/removed correctly.

### TC-FN-12: Log window live refresh
- Steps:
  1. Open View Log.
  2. Trigger compression event.
- Expected:
  - New entries appear within 2 seconds.
  - Severity coloring applies.

## 5. Failure and Recovery Tests

### TC-FR-01: Locked file retry
- Steps:
  1. Keep a target image locked by another process.
  2. Let watcher enqueue it.
- Expected:
  - Retry attempts happen with backoff.
  - No app crash.

### TC-FR-02: Corrupt image input
- Steps:
  1. Add corrupt image file.
- Expected:
  - Error logged for that file.
  - Queue continues processing next files.

### TC-FR-03: Temp file cleanup
- Steps:
  1. Create fake *.photoc.tmp in watched folder.
  2. Restart app.
- Expected:
  - Orphan temp files are swept on startup.

## 6. Performance Checks

### TC-PF-01: Bulk import
- Steps:
  1. Copy 100 mixed images into watched folder.
- Expected:
  - Queue depth rises then drains to zero.
  - App remains responsive.
  - No unhandled exceptions.

## 7. Test Report Template

Use this for each run:

- Build: Pass/Fail
- Startup: Pass/Fail
- Regression TC-RG-01: Pass/Fail
- Regression TC-RG-02: Pass/Fail
- Functional (FN): Passed X / Total 12
- Failure/Recovery (FR): Passed X / Total 3
- Performance (PF): Pass/Fail
- Notes:
