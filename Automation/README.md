# Patarik AI OS — Automation Host

This small companion program keeps the scheduled Telegram operations alive without requiring the Patarik AI OS window to remain open.

## What it does

- starts Patarik AI OS with `--automation` in a hidden background mode;
- restarts it 15 seconds after a crash;
- writes a heartbeat and a log to `%LocalAppData%\PatarikAIOS\automation`;
- uses a single-process lease: if the Automation Host is working, an opened desktop window does **not** send duplicate Telegram messages.

## Local rollout

1. Run `Publish-LocalAutomation.ps1` from this folder. It creates an `Automation\publish` folder.
2. Run `Install-LocalAutomation.ps1`, giving it `publish\App\PatarikAIOS.exe` and `publish\Automation`. It adds a Windows Task Scheduler entry with trigger **At log on** and restart-on-failure enabled.

The office computer must remain powered on and connected to the Internet. For operation during a power outage or when nobody signs in, move this host to a cloud server and replace local JSON storage with a shared database.
