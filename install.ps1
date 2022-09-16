dotnet pack tsgen/tsgen.csproj -c Release -o nupkg
dotnet tool install --add-source ./nupkg -g tsgen