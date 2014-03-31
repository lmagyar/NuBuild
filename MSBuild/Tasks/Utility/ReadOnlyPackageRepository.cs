﻿//===========================================================================
// MODULE:  ReadOnlyPackageRepository.cs
// PURPOSE: Immutable package repository
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
using NuGet;
// Project References

namespace NuBuild.MSBuild
{
   public class ReadOnlyPackageRepository : PackageRepositoryBase
   {
      private readonly IEnumerable<IPackage> packages;

      public ReadOnlyPackageRepository(IEnumerable<IPackage> packages)
      {
         this.packages = packages;
      }

      public override string Source
      {
         get { return null; }
      }

      public override bool SupportsPrereleasePackages
      {
         get { return true; }
      }

      public override IQueryable<IPackage> GetPackages()
      {
         return packages.AsQueryable();
      }
   }
}