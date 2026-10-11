# Launches the package through PLAY-BOARD-HEX-PREVIEW.cmd, waits for the main menu, captures only the
# game window to reviews/interrupt-cues-2026-10-11/launch-menu.png, then closes the game. No input is sent to it.
$pkg = "C:\Users\mattj\OneDrive\Documents\AI Dev\Infinite Conquest\playtest\unity-build-board-2026-10-11"
$shot = "C:\Users\mattj\OneDrive\Documents\AI Dev\Infinite Conquest\reviews\board-2026-10-11\launch-menu-hex.png"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W { [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Ri, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags); }
"@
$launched = Get-Date; Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$pkg\PLAY-BOARD-HEX-PREVIEW.cmd`"" -WorkingDirectory $pkg
$proc = $null
for ($i = 0; $i -lt 60 -and $proc -eq $null; $i++) { Start-Sleep -Seconds 1; $proc = Get-Process InfiniteConquestPlaytest -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -and $_.Path -eq "$pkg\InfiniteConquestPlaytest.exe" -and $_.StartTime -ge $launched } | Select-Object -First 1 }
if ($proc -eq $null) { "NO_WINDOW"; exit 1 }
"started pid $($proc.Id) path $($proc.Path)"
Start-Sleep -Seconds 15
$proc.Refresh()
[W]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Seconds 2
$r = New-Object W+R; [W]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
$w = $r.Ri - $r.L; $h = $r.B - $r.T
"window $w x $h at $($r.L),$($r.T); responding=$($proc.Responding)"
$bmp = New-Object System.Drawing.Bitmap $w, $h
# PrintWindow renders only the game window itself (PW_RENDERFULLCONTENT), never whatever is in front of it.
$g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc(); $ok = [W]::PrintWindow($proc.MainWindowHandle, $hdc, 2); $g.ReleaseHdc($hdc); $g.Dispose()
"printwindow ok=$ok"
$bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"saved $shot"
$proc.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 3
if (!$proc.HasExited) { Stop-Process -Id $proc.Id -Force }
"closed"

