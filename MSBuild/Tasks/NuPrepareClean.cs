//===========================================================================
// MODULE:  NuPrepareClean.cs
// PURPOSE: NuBuild prepare MSBuild task for clean
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
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
// Project References

namespace NuBuild.MSBuild
{
   /// <summary>
   /// Prepare task
   /// </summary>
   /// <remarks>
   /// This task configures the NuBuild Compile items for packaging,
   /// by attaching custom metadata and determining the build sources/
   /// targets for incremental builds. The task also establishes the
   /// build/version number for the current run.
   /// </remarks>
   public sealed class NuPrepareClean : Task
   {
      private List<ITaskItem> targetList = new List<ITaskItem>();

      #region Task Parameters
      /// <summary>
      /// The full project path
      /// </summary>
      [Required]
      public String ProjectPath { get; set; }
      /// <summary>
      /// The project output directory path
      /// </summary>
      [Required]
      public String OutputPath { get; set; }
      /// <summary>
      /// Return the list of build target files via here
      /// </summary>
      [Output]
      public ITaskItem[] Targets { get; set; }
      #endregion

      /// <summary>
      /// Task execution override
      /// </summary>
      /// <returns>
      /// True if successful
      /// False otherwise
      /// </returns>
      public override Boolean Execute ()
      {
         try
         {
            // parepare the task for execution
            if (!Path.IsPathRooted(this.OutputPath))
               this.OutputPath = Path.GetFullPath(this.OutputPath);

            // read in .nupkgs intermediate file
            // MsBuild can't cache these projects (no binary output), these reference information are stored in intermediate files
            // and we can read it back for clean targets
            var nupkgsFullPath = ProjectHelper.GetNupkgsFullPath(ProjectPath, OutputPath);
            if (File.Exists(nupkgsFullPath))
            {
               this.targetList.AddRange(System.IO.File.ReadAllLines(nupkgsFullPath)
                  .AsEnumerable()
                  .Select(pkgPath => new TaskItem(pkgPath)));
               this.targetList.Add(new TaskItem(nupkgsFullPath));
            }
            
            // return the list of clean targets
            this.Targets = this.targetList.ToArray();
         }
         catch (Exception e)
         {
            Log.LogError("{0} ({1})", e.ToString(), e.GetType().Name);
            return false;
         }
         return true;
      }
   }
}
