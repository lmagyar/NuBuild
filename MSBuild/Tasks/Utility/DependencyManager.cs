//===========================================================================
// MODULE:  DependencyManager.cs
// PURPOSE: Calculate frameworkAssemblies and dependencies information
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
// System References
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using NuGet;
using System.Collections.ObjectModel;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
// Project References

namespace NuBuild.MSBuild
{
   public static class SemanticVersionExtensions
   {
      public static SemanticVersion GetLimitingMajor(this SemanticVersion semanticVersion)
      {
         // do not modify the test below, due to SemanticVersion.NormalizeVersionValue() in SemanticVersion ctor
         if (semanticVersion.ToString().Count(c => c == '.') > 2)
            return new SemanticVersion(semanticVersion.Version.Major + 1, 0, 0, 0);
         else
            return new SemanticVersion(semanticVersion.Version.Major + 1, 0, 0, null);
      }
   }

   public class DependencyManager
   {
      protected String targetFrameworkMoniker;
      protected Boolean addBinariesToSubfolder;
      protected Boolean limitMajorVersionOfDependencies;
      protected TaskLoggingHelper log;

      protected HashSet<ProjectFactory> referenceProjectFactories;
      protected HashSet<ProjectFactory> nuBuildReferenceProjectFactories;
      protected HashSet<FrameworkName> targetFrameworks;
      protected Dictionary<FrameworkName, HashSet<String>> frameworkAssembliesByFramework;
      protected Dictionary<FrameworkName, Dictionary<String, Tuple<IPackage, PackageDependency>>> packagesAndDependenciesByFramework;

      protected Collection<FrameworkAssemblyReference> frameworkReferences;
      protected Collection<PackageDependencySet> dependencySets;

      public DependencyManager(String targetFrameworkMoniker, Boolean addBinariesToSubfolder, Boolean limitMajorVersionOfDependencies, TaskLoggingHelper log)
      {
         this.targetFrameworkMoniker = targetFrameworkMoniker;
         this.addBinariesToSubfolder = addBinariesToSubfolder;
         this.limitMajorVersionOfDependencies = limitMajorVersionOfDependencies;
         this.log = log;
      }

      public Collection<FrameworkAssemblyReference> FrameworkReferences { get { return frameworkReferences; } }
      public Collection<PackageDependencySet> DependencySets { get { return dependencySets; } }

      public void CalculateMinimalSet(ITaskItem[] referenceProjects, PackageBuilder builder)
      {
         InitializeVariables();
         LoadReferences(referenceProjects);
         ProcessReferenceInformation();
         CompactReferenceInformation(builder);
         ReorganizeResults();
         LogResults();
      }

      protected void InitializeVariables()
      {
         ProjectFactoryEqualityComparer projectFactoryEqualityComparer = new ProjectFactoryEqualityComparer();
         referenceProjectFactories = new HashSet<ProjectFactory>(projectFactoryEqualityComparer);
         nuBuildReferenceProjectFactories = new HashSet<ProjectFactory>(projectFactoryEqualityComparer);
         targetFrameworks = new HashSet<FrameworkName>();
         frameworkAssembliesByFramework = new Dictionary<FrameworkName, HashSet<String>>();
         packagesAndDependenciesByFramework = new Dictionary<FrameworkName, Dictionary<String, Tuple<IPackage, PackageDependency>>>();
      }

      protected void LoadReferences(ITaskItem[] referenceProjects)
      {
         // collecting reference information to variables
         foreach (var prjPath in referenceProjects
            .FullPath()
            .ValidProjectForDependencyCollection())
         {
            var prjFactory = new ProjectFactory(prjPath);
            prjFactory.CollectDependencies(referenceProjectFactories,
               targetFrameworks, frameworkAssembliesByFramework, packagesAndDependenciesByFramework);

            // debug info after each collection
            Debug.WriteLine("\n--- Project: {0} | {1} ---", prjFactory.TargetFramework, Path.GetDirectoryName(prjFactory.FullPath));
            Debug.WriteLine("referenceProjectFactories after CollectDependencies");
            Print(referenceProjectFactories);
            Debug.WriteLine("frameworkAssemblies after CollectDependencies");
            Print(frameworkAssembliesByFramework);
            Debug.WriteLine("packagesAndDependencies after CollectDependencies");
            Print(packagesAndDependenciesByFramework);
         }
         // if there are no binaries, use the packager's framework as placeholder
         if (targetFrameworks.Count == 0)
         {
            var targetFramework = new FrameworkName(targetFrameworkMoniker);
            targetFrameworks.Add(targetFramework);
            frameworkAssembliesByFramework.Add(targetFramework,
               new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            packagesAndDependenciesByFramework.Add(targetFramework,
               new Dictionary<string, Tuple<IPackage, PackageDependency>>());
         }
         // collecting nubuild reference information to variables
         foreach (var prjPath in referenceProjects
            .FullPath()
            .ValidProjectForNuBuildDependencyCollection())
         {
            var prjFactory = new ProjectFactory(prjPath);
            prjFactory.CollectNuBuildDependencies(nuBuildReferenceProjectFactories);

            // debug info after each collection
            Debug.WriteLine("\n--- Project: {0} | {1} ---", prjFactory.TargetFramework, Path.GetDirectoryName(prjFactory.FullPath));
            Debug.WriteLine("nuBuildReferenceProjectFactories after CollectNuBuildDependencies");
            Print(nuBuildReferenceProjectFactories);
         }
      }

      protected void ProcessReferenceInformation()
      {
         Debug.WriteLine("\n--- Handling project references ---");
         var optimizedZipPackages = new Dictionary<string, OptimizedZipPackage>();

         Debug.WriteLine("adding referred NuBuild projects to dependencies by each targetFramework");
         foreach (var prjFactory in nuBuildReferenceProjectFactories)
         {
            Debug.WriteLine(" project: {0}", (object)Path.GetFileName(prjFactory.FullPath));
            foreach (var nuTarget in prjFactory.NuTargets)
            {
               Debug.WriteLine("  package: {0}", (object)Path.GetFileName(nuTarget));
               OptimizedZipPackage package;
               if (!optimizedZipPackages.TryGetValue(nuTarget, out package))
                  optimizedZipPackages.Add(nuTarget, package = new OptimizedZipPackage(nuTarget));
               var dependency = new PackageDependency(package.Id, new VersionSpec()
               {
                  IsMinInclusive = true,
                  MinVersion = package.Version,
                  MaxVersion = limitMajorVersionOfDependencies ? package.Version.GetLimitingMajor() : null,
               });
               foreach (var targetFramework in targetFrameworks)
               {
                  Debug.WriteLine("   dependency: {0} | {1} | {2}", targetFramework, dependency.Id, dependency.VersionSpec);
                  packagesAndDependenciesByFramework[targetFramework].Add(
                     package.Id, new Tuple<IPackage, PackageDependency>(package, dependency));
               }
            }
         }
      }

      protected void CompactReferenceInformation(PackageBuilder builder)
      {
         Debug.WriteLine("\n--- Compacting ---");
         dependencySets = new Collection<PackageDependencySet>();
         // reserve only the minimum required dependency information by target framework
         var firstDependenciesAsString = default(string[]);
         foreach (var targetFramework in targetFrameworks)
         {
            Debug.WriteLine("{0}", targetFramework);
            var frameworkAssemblies = frameworkAssembliesByFramework[targetFramework];
            var packagesAndDependencies = packagesAndDependenciesByFramework[targetFramework];
            var packages = packagesAndDependencies.Values.Select(t => t.Item1).ToList();

            // removing FrameworkAssemblies referenced by packages
            Debug.WriteLine(" removing referenced FrameworkAssemblies");
            foreach (var package in packages)
            {
               Debug.WriteLine("  package: {0}", (object)(!string.IsNullOrEmpty(package.Title) ? package.Title : package.Id));
               foreach (var frameworkAssembly in package.FrameworkAssemblies)
               {
                  Debug.WriteLine("   ref: {0}", (object)frameworkAssembly.AssemblyName);
                  if (frameworkAssembly.SupportedFrameworks.Count() == 0 || frameworkAssembly.SupportedFrameworks.Count(fn => fn == targetFramework) != 0)
                     frameworkAssemblies.Remove(frameworkAssembly.AssemblyName);
               }
            }

            // reserve only the minimum set of packages (original NuGet algorythm)
            var dependencies = builder
               .GetCompatiblePackageDependencies(targetFramework)
               .ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
            // Reduce the set of packages we want to include as dependencies to the minimal set.
            // Normally, packages.config has the full closure included, we only add top level
            // packages, i.e. packages with in-degree 0
            foreach (var package in MinimalSetWalker.GetMinimalSet(packages, targetFramework))
            {
               // Don't add duplicate dependencies
               if (dependencies.ContainsKey(package.Id))
                  continue;
               var dependency = packagesAndDependencies[package.Id].Item2;
               if (dependency.VersionSpec.MaxVersion == null && limitMajorVersionOfDependencies)
                  dependency = new PackageDependency(dependency.Id, new VersionSpec()
                     {
                        IsMinInclusive = dependency.VersionSpec.IsMinInclusive,
                        MinVersion = dependency.VersionSpec.MinVersion,
                        MaxVersion = dependency.VersionSpec.MinVersion.GetLimitingMajor()
                     });
               dependencies[dependency.Id] = dependency;
            }

            // check to fit in 1 dependencySet if required
            var addToDependencySets = true;
            if (!addBinariesToSubfolder)
            {
               if (firstDependenciesAsString == null)
                  firstDependenciesAsString = dependencies.Select(kvp => kvp.Value.ToString()).ToArray();
               else if (firstDependenciesAsString.Length != dependencies.Count ||
                  dependencies.Values.Any(d => !firstDependenciesAsString.Contains(d.ToString())))
               {
                  var dependenciesAsString = dependencies.Values.Select(d => d.ToString()).ToArray();
                  var dependencyMismatch = default(string);
                  if (firstDependenciesAsString.Length <= dependenciesAsString.Length)
                     dependencyMismatch = dependenciesAsString.Where(d => !firstDependenciesAsString.Contains(d)).First();
                  else
                     dependencyMismatch = firstDependenciesAsString.Where(d => !dependenciesAsString.Contains(d)).First();
                  log.LogError("Dependency '{0}' is not referred from each targetFramework, can't add dependency without enabling multiple frameworks in package.", (object)dependencyMismatch);
                  return;
               }
               else
                  addToDependencySets = false;
            }

            // add to dependency sets
            if (addToDependencySets)
            {
               if (!addBinariesToSubfolder)
                  dependencySets.Add(new PackageDependencySet(null, dependencies.Values));
               else if (dependencies.Values.Count != 0)
                  dependencySets.Add(new PackageDependencySet(targetFramework, dependencies.Values));
            }
         }
      }

      protected void ReorganizeResults()
      {
         // reorganise assembly references from "assembly by framework" to "framework by assembly" structure
         var targetFrameworksCount = targetFrameworks.Count;
         var allFrameworkAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         foreach (var assemblies in frameworkAssembliesByFramework.Values)
            foreach (var assembly in assemblies)
               allFrameworkAssemblies.Add(assembly);
         frameworkReferences = new Collection<FrameworkAssemblyReference>();
         foreach (var assembly in allFrameworkAssemblies)
         {
            var usedInTargetFrameworks = targetFrameworks
               .Where(fn => frameworkAssembliesByFramework[fn].Contains(assembly))
               .ToList();
            if (targetFrameworksCount == 1 || usedInTargetFrameworks.Count == targetFrameworksCount)
               frameworkReferences.Add(new FrameworkAssemblyReference(assembly));
            else
            {
               if (!addBinariesToSubfolder)
               {
                  log.LogError("Assembly '{0}' is not referred from each targetFramework, can't add frameworkReference without enabling multiple frameworks in package.", (object)assembly);
                  return;
               }
               frameworkReferences.Add(new FrameworkAssemblyReference(assembly, usedInTargetFrameworks));
            }
         }
      }

      protected void LogResults()
      {
         Debug.WriteLine("\n--- Final results ---");
         // log final info
         LogMessage(frameworkReferences);
         LogMessage(dependencySets);
      }

      #region Printing DEBUG info

      [Conditional("DEBUG")]
      private void Print(HashSet<ProjectFactory> referenceProjectFactories)
      {
         foreach (var referenceProjectFactory in referenceProjectFactories)
            Debug.WriteLine(" {0} | {1}", referenceProjectFactory.TargetFramework, Path.GetFileName(referenceProjectFactory.FullPath));
      }

      [Conditional("DEBUG")]
      private void Print(Dictionary<FrameworkName, HashSet<string>> frameworkAssembliesByFramework)
      {
         foreach (var frameworkAssemblyByFramework in frameworkAssembliesByFramework)
         {
            Debug.WriteLine(" {0}", (object)frameworkAssemblyByFramework.Key);
            foreach (var frameworkAssembly in frameworkAssemblyByFramework.Value)
                  Debug.WriteLine("  {0}", (object)frameworkAssembly);
         }
      }

      [Conditional("DEBUG")]
      private void Print(Dictionary<FrameworkName, Dictionary<String, Tuple<IPackage, PackageDependency>>> packagesAndDependenciesByFramework)
      {
         foreach (var packagesAndDependencyByFramework in packagesAndDependenciesByFramework)
         {
            Debug.WriteLine(" {0}", (object)packagesAndDependencyByFramework.Key);
            foreach (var packagesAndDependency in packagesAndDependencyByFramework.Value)
                  Debug.WriteLine("  {0} | {1} | {2} | {3}", packagesAndDependency.Key, packagesAndDependency.Value.Item1.Title, packagesAndDependency.Value.Item2.VersionSpec.MinVersion, packagesAndDependency.Value.Item2.VersionSpec.MaxVersion);
         }
      }

      #endregion

      private void LogMessage(Collection<FrameworkAssemblyReference> frameworkReferences)
      {
         Debug.WriteLine("frameworkAssemblies after removing referred package's FrameworkAssemblies");
         if (frameworkReferences.Count > 0)
            log.LogMessage(MessageImportance.High, "Auto <frameworkAssemblies>");
         foreach (var far in frameworkReferences)
         {
            var sb = new StringBuilder();
            sb.Append(" ");
            sb.Append(far.AssemblyName);
            sb.Append(" | ");
            bool first = true;
            foreach (var fn in far.SupportedFrameworks)
            {
                  if (first)
                     first = false;
                  else
                     sb.Append(", ");
                  sb.Append(fn.FullName);
            }
            var message = sb.ToString();
            Debug.WriteLine(message);
            log.LogMessage(MessageImportance.High, message);
         }
      }

      private void LogMessage(Collection<PackageDependencySet> dependencySets)
      {
         Debug.WriteLine("packagesAndDependencies minimum set with referred top level packages");
         if (dependencySets.Count > 0)
            log.LogMessage(MessageImportance.High, "Auto <dependencies>");
         foreach (var pds in dependencySets)
         {
            var sb = new StringBuilder();
            sb.Append(" ");
            sb.Append(pds.TargetFramework == null ? "framework independent" : pds.TargetFramework.ToString());
            foreach (var pd in pds.Dependencies)
            {
                  sb.Append("\n  ");
                  sb.Append(pd.Id);
                  sb.Append(" | ");
                  sb.Append(pd.VersionSpec.ToString());
            }
            var message = sb.ToString();
            Debug.WriteLine(message);
            log.LogMessage(MessageImportance.High, message);
         }
      }

   }
}
