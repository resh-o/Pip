# DeskPet

A Windows desktop pet that keeps you on task using body language, and by dragging other apps' windows around.

**Tests:** from the repo root in PowerShell, run `wsl -d Ubuntu-24.04 -- bash -c 'rm -rf ~/deskpet && mkdir ~/deskpet && tar --exclude=bin --exclude=obj -cf - Directory.Build.props .editorconfig src tests | tar -xf - -C ~/deskpet && cd ~/deskpet && dotnet test tests/DeskPet.Core.Tests'`. This runs them under WSL2 with the .NET 8 SDK, because Smart App Control blocks unsigned test DLLs on Windows.
