Servicio : dotnet run --project src/Service

App : dotnet run --project src/UI

## Compilar ejecutables

Servicio : dotnet publish src/Service -c Release -r win-x64 --self-contained false -o build/Service

App : dotnet publish src/UI -c Release -r win-x64 --self-contained false -o build/UI