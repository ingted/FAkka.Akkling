param(
    $assembly
)
write-host ($assembly + ': Current path: ' + (pwd).path)
cd ./bin
try {
    $pkg = (dir "$($assembly)*.nupkg" | Sort-Object -Property Name -Descending)[0].Name
    invoke-expression "dotnet nuget push $pkg --api-key $(gc /Nuget/apikey.txt) --source https://api.nuget.org/v3/index.json"
}
catch {
    write-host "=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+="
    write-host $_
    write-host "=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+="
}
