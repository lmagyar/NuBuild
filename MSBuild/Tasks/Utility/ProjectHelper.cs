//===========================================================================
// MODULE:  ProjectHelper.cs
// PURPOSE: ProjectFactory helper
// 
// Copyright © 2012
// Brent M. Spell. All rights reserved.
//
// This library is free software; you can redistribute it and/or modify it 
// under the terms of the GNU Lesser General Public License as published 
// by the Free Software Foundation; either version 3 of the License, or 
// (at your option) any later version. This library is distributed in the 
// hope that it will be useful, but WITHOUT ANY WARRANTY; without even the 
// implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
// See the GNU Lesser General Public License for more details. You should 
// have received a copy of the GNU Lesser General Public License along with 
// this library; if not, write to 
//    Free Software Foundation, Inc. 
//    51 Franklin Street, Fifth Floor 
//    Boston, MA 02110-1301 USA
//===========================================================================
// this class is based on http://nuget.codeplex.com/
//===========================================================================
// System References
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using NuGet;
// Project References

namespace NuBuild.MSBuild
{
   public static class ProjectHelper
   {
      private static readonly HashSet<string> supportedProjectExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {  
         ".csproj",
         ".vbproj",
         ".fsproj",
      };

      private static readonly List<string> csDefaultReferences = new List<string> {
         // TODO get default references for other than basic library CS projects
         "Microsoft.CSharp", 
         "System", 
         "System.Core", 
         "System.Data", 
         "System.Data.DataSetExtensions", 
         "System.Xml", 
         "System.Xml.Linq",
         "System.Net",
         "System.Windows",
         "mscorlib.extensions",
      };

      private static readonly List<string> vbDefaultReferences = new List<string> {
         // TODO get default references for basic and other library VB projects
         "System", 
      };

      private static readonly List<string> fsDefaultReferences = new List<string> {
         // TODO get default references for other than basic library FS projects
         "FSharp.Core",
         "mscorlib",
         "System", 
         "System.Core", 
         "System.Numerics",
      };

      public static IEnumerable<ProjectItem> GetItems(this Project project, string itemType1, string itemType2)
      {
         return project.GetItems(itemType1).Concat(project.GetItems(itemType2));
      }

      public static string FullPath(this ITaskItem taskItem)
      {
         return taskItem.GetMetadata("FullPath");
      }

      public static string MSBuildSourceProjectFile(this ITaskItem taskItem)
      {
         return taskItem.GetMetadata("MSBuildSourceProjectFile");
      }

      public static bool ValidReferenceLibraryForBinaryNuSource(ITaskItem referenceLibrary)
      {
         return !ValidProjectForNuBuildDependencyCollection(referenceLibrary.MSBuildSourceProjectFile());
      }

      public static IEnumerable<ITaskItem> ValidReferenceLibraryForBinaryNuSource(this IEnumerable<ITaskItem> referenceLibraries)
      {
         return referenceLibraries
            .Where(referenceLibrary => ValidReferenceLibraryForBinaryNuSource(referenceLibrary));
      }

      public static bool ValidReferenceLibraryForPropertyProvider(ITaskItem referenceLibrary)
      {
         var copyLocal = referenceLibrary.GetMetadata("Private");
         return (String.IsNullOrEmpty(copyLocal) || String.Compare(copyLocal, "false", true) != 0);
      }

      public static IEnumerable<ITaskItem> ValidReferenceLibraryForPropertyProvider(this IEnumerable<ITaskItem> referenceLibraries)
      {
         return referenceLibraries
            .Where(libraryReference => ValidReferenceLibraryForPropertyProvider(libraryReference));
      }

      public static bool ValidReferenceLibraryForNuBuildNuSource(ITaskItem referenceLibrary)
      {
         return ValidProjectForNuBuildDependencyCollection(referenceLibrary.MSBuildSourceProjectFile());
      }

      public static IEnumerable<ITaskItem> ValidReferenceLibraryForNuBuildNuSource(this IEnumerable<ITaskItem> referenceLibraries)
      {
         return referenceLibraries
            .Where(referenceLibrary => ValidReferenceLibraryForNuBuildNuSource(referenceLibrary));
      }

      public static bool ValidProjectForDependencyCollection(string projectFile)
      {
         return !string.IsNullOrEmpty(projectFile) &&
            ProjectHelper.IsSupportedProject(projectFile) &&
            !ProjectHelper.HasNuspecFile(projectFile);
      }

      public static bool ValidProjectForDependencyCollection(this ITaskItem referenceProject)
      {
         return ValidProjectForDependencyCollection(referenceProject.FullPath());
      }

      public static IEnumerable<ITaskItem> ValidProjectForDependencyCollection(this IEnumerable<ITaskItem> referenceProjects)
      {
         return referenceProjects
            .Where(referenceProject => ValidProjectForDependencyCollection(referenceProject));
      }

      public static bool ValidProjectForNuBuildDependencyCollection(string projectFile)
      {
         return HasNuspecFile(projectFile);
      }

      public static bool ValidProjectForNuBuildDependencyCollection(this ITaskItem referenceProject)
      {
         return ValidProjectForNuBuildDependencyCollection(referenceProject.FullPath());
      }

      public static IEnumerable<ITaskItem> ValidProjectForNuBuildDependencyCollection(this IEnumerable<ITaskItem> referenceProjects)
      {
         return referenceProjects
            .Where(referenceProject => ValidProjectForNuBuildDependencyCollection(referenceProject));
      }

      public static IEnumerable<string> UnappliedItem(this IEnumerable<string> items, HashSet<string> alreadyAppliedProjects)
      {
         return items
            .Where(fullPath => !alreadyAppliedProjects.Contains(fullPath));
      }
      
      public static bool IsSupportedProject(string projectFile)
      {
         var extension = Path.GetExtension(projectFile);
         return supportedProjectExtensions.Contains(extension);
      }

      public static IEnumerable<string> GetDefaultReferences(string projectFile)
      {
         if (projectFile.EndsWith(".csproj"))
            return csDefaultReferences;
         else if (projectFile.EndsWith(".vbproj"))
            return vbDefaultReferences;
         else if (projectFile.EndsWith(".fsproj"))
            return fsDefaultReferences;
         else
            return Enumerable.Empty<string>();
      }

      public static string GetNupkgsFullPath(string projectFullPath, string outputPath)
      {
         return Path.Combine(
            outputPath,
            Path.GetFileNameWithoutExtension(projectFullPath) + ".nupkgs");
      }

      public static string GetSolutionDir(string projectDirectory)
      {
         var path = projectDirectory;

         // Only look 4 folders up to find the solution directory
         const int maxDepth = 5;
         int depth = 0;
         do
         {
            if (SolutionFileExists(path))
               return path;

            path = Path.GetDirectoryName(path);

            depth++;
            //When you get to c:\, the parent path is null.
         } while (depth < maxDepth && path != null);

         return null;
      }

      private static bool SolutionFileExists(string path)
      {
         return Directory.GetFiles(path, "*.sln").Any();
      }

      private static bool HasNuspecFile(string projectFile)
      {
         return Directory
            .EnumerateFiles(
               Path.GetDirectoryName(projectFile),
               "*" + Constants.ManifestExtension,
               SearchOption.AllDirectories)
            .Any();
      }
   }
}
