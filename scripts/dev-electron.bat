@echo off
cd /d %~dp0\..
call npm run build:core:dev
call npm run build:electron
start cmd /k npm run dev:renderer
start cmd /k npm run dev:electron:fast
