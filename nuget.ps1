dir *.nupkg -recurse | ?{
	$_.name -match "^FAkka"
} | %{
	$pkg = $_.fullname
	invoke-expression "dotnet nuget push $pkg --api-key $(gc G:\Nuget\apikey.txt) --source https://api.nuget.org/v3/index.json"
	
}