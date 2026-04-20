dotnet build .\Gulla.Optimizely.Graph.Cms.Ui\Gulla.Optimizely.Graph.Cms.Ui.csproj -c Release
dotnet pack .\Gulla.Optimizely.Graph.Cms.Ui\Gulla.Optimizely.Graph.Cms.Ui.csproj -c Release

move .\Gulla.Optimizely.Graph.Cms.Ui\bin\Release\*.nupkg ..\..\Nuget
