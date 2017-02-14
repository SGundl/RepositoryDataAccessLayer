import groovy.io.FileType


node {
 ws("workspace/${env.JOB_NAME}") 
 {
	stage('Checkout')
	{
		checkout scm

	} 
def files = getFiles("workspace/${env.JOB_NAME}");
def pscmd = { String cmd ->
"powershell -NoProfile -ExecutionPolicy Bypass -Command \"cd $foldername; ${cmd}\""
}
def namespace = "Polaris.RepositoryDataAccessLayer."
	 
	for(int i=1; i < files.size(); i++)
	 {
		 foldername = files[i].getName()
		 def use = new File("workspace/${env.JOB_NAME}/$foldername//project.json")
		 //echo use.getAbsolutePath()
		 //echo use.exists().toString()
		 if(use.exists())
		 {
			 stage(foldername)
			 {
				bat (pscmd('$delim =[char]58;$versionstring = type ./project.json | findstr version;$a,$version = $versionstring.split($delim,2);$version.Substring(2,$version.Length-4) | out-file "version.txt" -encoding utf8'))

				version = readFile("$foldername/version.txt").trim().substring(3) //BOM
				if(version.equals("0.0.1"))
				{
				 
					currentver = "0.0.0"
					latestver = "0.0.1"
				 }
				 else
				 {
					latestver = bat(script:"nuget list $foldername -source http://10.193.128.100/nuget",returnStdout: true).trim().split("\\r?\\n")[1]
					currentver = "$foldername $version"
					currentver = namespace + currentver.replaceAll("\\P{Print}", "");
					latestver = latestver.replaceAll("\\P{Print}", "");
					echo currentver
					echo latestver
				 }
				if(!latestver.equals(currentver))
				{
					bat """
					cd ${foldername}
					dotnet restore project.json
					dotnet pack project.json -c Release -o ../Package

					"""

					withCredentials([[$class: 'StringBinding', credentialsId: 'NugetAPIKey', variable: 'NugetAPIKey']]) {
					if(env.BRANCH_NAME.contains("master"))
					{
						bat "nuget.exe push Package/Polaris.RepositoryDataAccessLayer.$foldername.${version}.nupkg %NugetAPIKey% -Source http://10.193.128.100/api/v2/package"
					}
					if(env.BRANCH_NAME.contains("develop"))
					{
						bat """
						cd Package
						rename Polaris.RepositoryDataAccessLayer.$foldername.${version}.nupkg Polaris.RepositoryDataAccessLayer.$foldername.${version}-beta.nupkg
						nuget.exe push Polaris.RepositoryDataAccessLayer.$foldername.${version}-beta.nupkg %NugetAPIKey% -Source http://10.193.128.100/api/v2/package
						"""
					}
				
				}
			}
				 else
				{
					//currentBuild.result = 'NOT_BUILT'
					echo "$foldername version not increased, skipping nuget publish"	
				}
		 }
	}
}
 }}
@NonCPS
def getFiles(workspace) {
  def files=[]
	(workspace as File).eachFile groovy.io.FileType.DIRECTORIES, {
    	it -> files << it
	}
	
return files
}
