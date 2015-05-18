
Each NuBuild project emits an additional .nupkgs file containing the list of the built .nupkg files.

This list is used in both the Clean and the Build process, because this list contains the exact filenames of the .nupkg files emitted by the NuBuild project.
The exact .nupkg file names can contain the version of the package, and the version can depend on different and complex conditions (eg. assembly version of 
other project's output added to a specific package).

Clean simply adds the list of the .nupkg files to the file list to be deleted.

Build uses this file from referred NuBuild projects to add the exact versions of the referred packages to the dependency list. The exact versions are determined by reading the
.nupkg files and parsing the contents of them.

Referring other NuBuild projects in a NuBuild project has a trick. NuBuild projects let VS to believe they emit an .exe, because VS only allows 
to add binary references to projects. In fact no .exe is emitted, only a .nupks file and the .nupkg files determined by the .nuspec files in the NuBuild project.

The ReferenceLibraries list contains the fake .exe files all the times, but the MSBuildSourceProjectFile metadata contains the original projects FullPath metadata in the 
ReferenceProjects list. This way we can navigate to the original projects and we can also determine the real output file name (always <projectname>.nupkgs).
