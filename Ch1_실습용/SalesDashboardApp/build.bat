@echo off
rem Build SalesDashboard.exe with the C# compiler bundled in Windows (.NET Framework 4). No install needed.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /out:SalesDashboard.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /codepage:65001 Models.cs XlsxReader.cs Widgets.cs PdfWriter.cs DashboardRenderer.cs MainForm.cs Program.cs
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK: SalesDashboard.exe
