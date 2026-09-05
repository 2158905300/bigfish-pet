@echo off
rem BigFishPet - one-click DeepSeek API key setup.
rem Saves to User environment variables and broadcasts refresh,
rem so new processes (BigFishPet.exe) can read it right away.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$k = Read-Host 'Paste your DeepSeek API key (sk-...) and press Enter'; if ([string]::IsNullOrWhiteSpace($k)) { Write-Host '[FAIL] empty input, nothing changed.'; exit 1 }; [Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', $k, 'User'); Write-Host '[OK] DEEPSEEK_API_KEY saved (User env, refresh broadcast).'; Write-Host 'Now close and restart BigFishPet.exe to enable AI chat.'"
echo.
pause
