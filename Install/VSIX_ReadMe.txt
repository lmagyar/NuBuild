
You can't install .vsix files with VSExtension:VsixPackage (it uses VSIXInstaller.exe), because
InstalledByMsi is set to true in .vsixmanifest to gray out the uninstall button in VS, but this
also prevents to use VSIXInstaller to install it.

This direct call also won't work for the same reason:

      <Property Id="VSINSTALLDIR">
         <RegistrySearch Id="VSInstallRegistry" Root="HKLM" Key="SOFTWARE\Microsoft\VisualStudio\12.0" Name="InstallDir"  Type="directory" />
      </Property>
      <CustomAction Id="SetVSIXInstaller" Return="check" Execute="immediate" Property="VSIXInstaller" Value="[VSINSTALLDIR]xVSIXInstaller.exe" />
      <CustomAction Id="DeployVSIX" Property="VSIXInstaller" Execute="deferred" Impersonate="no" ExeCommand="/quiet" Return="asyncWait"/>
      <InstallExecuteSequence>
         <Custom Action="DeployVSIX" After="MsiPublishAssemblies" />
      </InstallExecuteSequence>

The .vsix version is below (for documentation purposes only):

<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
   <Fragment Id="VS_Extensions">
      <DirectoryRef Id="TARGETDIR">
         <!-- predefined folder -->
         <Directory Id="TempFolder">
            <Component Id="VS_NuBuild_VS_Package_vsix" Guid="*">
               <!-- PackageId must equal to vsixmanifest's Identifier Id -->
               <VSExtension:VsixPackage File="VS_NuBuild_VS_Package_vsix" PackageId="d4cda160-3cb8-4614-a9ed-f845cb2ddc64" Vital="yes" Permanent="no" />
               <File Id="VS_NuBuild_VS_Package_vsix" Source="..\Bin\NuBuild.VS.Package.vsix" KeyPath="yes"/>
            </Component>
         </Directory>
      </DirectoryRef>
      <!-- group the components, in case there will be multiple components in the future -->
      <ComponentGroup Id="VS_Extensions">
         <ComponentRef Id="VS_NuBuild_VS_Package_vsix"/>
      </ComponentGroup>
      <!-- reference this feature from Product -->
      <Feature Id="VS_Extensions" Level="1"
               Title="VS Extensions and Project Templates"
               Description="Integrates NuBuild extensions and project templates into Visual Studio.">
         <ComponentGroupRef Id="VS_Extensions" />
      </Feature>
   </Fragment>
</Wix>
