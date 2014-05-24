//===========================================================================
// MODULE:  NuBuildPropertyPage.cs
// PURPOSE: NuBuild project property storage
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
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
// Project References
using CSVSXProjectSubType.PropertyPageBase;

namespace NuBuild.VS
{
   public class NuBuildPropertyPagePropertyStore : IPropertyStore
   {
      private List<string> configs;
      private IVsBuildPropertyStorage buildPropStorage;

      public event StoreChangedDelegate StoreChanged;

      #region IPropertyStore Members
      /// <summary>
      /// Use the data passed in to initialize the Properties. 
      /// </summary>
      /// <param name="dataObject">
      /// This is normally only one our configuration object, which means that 
      /// there will be only one elements in configs.
      /// If it is null, we should release it.
      /// </param>
      public void Initialize(object[] dataObjects)
      {
         // If we are editing multiple configuration at once, we may get multiple objects.
         foreach (object dataObject in dataObjects)
         {
            if (dataObject is IVsBrowseObject)
            {
               // Project properties page
               IVsBrowseObject browseObject = dataObject as IVsBrowseObject;
               IVsHierarchy pHier;
               uint pItemid;
               ErrorHandler.ThrowOnFailure(browseObject.GetProjectItem(out pHier, out pItemid));
               buildPropStorage = (IVsBuildPropertyStorage)pHier;
               break;
            }
            else if (dataObject is IVsCfgBrowseObject)
            {
               // Configuration dependent properties page
               if (buildPropStorage == null)
               {
                  IVsCfgBrowseObject browseObject = dataObject as IVsCfgBrowseObject;
                  IVsHierarchy pHier;
                  uint pItemid;
                  ErrorHandler.ThrowOnFailure(browseObject.GetProjectItem(out pHier, out pItemid));
                  buildPropStorage = (IVsBuildPropertyStorage)pHier;
               }
               if (configs == null)
                  configs = new List<string>();
               string config;
               ErrorHandler.ThrowOnFailure((dataObject as IVsCfg).get_DisplayName(out config));
               configs.Add(config);
            }
         }
      }

      /// <summary>
      /// Set the value of the specified property in storage.
      /// </summary>
      /// <param name="propertyName">Name of the property to set.</param>
      /// <param name="propertyValue">Value to set the property to.</param>
      public void SetPropertyValue(string propertyName, string propertyValue)
      {
         // Handle multiple properties
         string[] propertyNames = propertyName.Split(',');
         string[] propertyValues;
         if (propertyValue == null)
         {
            propertyValues = new string[propertyNames.Length];
            for (int i = 0; i < propertyNames.Length; i++)
               propertyValues[i] = string.Empty;
         }
         else
            propertyValues = propertyValue.Split(',');

         if (configs == null)
            for (int i = 0; i < propertyNames.Length; i++)
               ErrorHandler.ThrowOnFailure(buildPropStorage.SetPropertyValue(propertyNames[i], String.Empty, (uint)_PersistStorageType.PST_PROJECT_FILE, propertyValues[i]));
         else
            foreach (string config in configs)
               for (int i = 0; i < propertyNames.Length; i++)
                  ErrorHandler.ThrowOnFailure(buildPropStorage.SetPropertyValue(propertyNames[i], config, (uint)_PersistStorageType.PST_PROJECT_FILE, propertyValues[i]));
         if (StoreChanged != null)
         {
            StoreChanged();
         }
      }

      /// <summary>
      /// Retreive the value of the specified property from storage
      /// </summary>
      /// <param name="propertyName">Name of the property to retrieve</param>
      /// <returns></returns>
      public string GetPropertyValue(string propertyName)
      {
         // Handle multiple properties
         string[] propertyNames = propertyName.Split(',');
         string[] propertyValues = new string[propertyNames.Length];
         bool initialized = false;

         if (configs == null)
            for (int i = 0; i < propertyNames.Length; i++)
               buildPropStorage.GetPropertyValue(propertyNames[i], String.Empty, (uint)_PersistStorageType.PST_PROJECT_FILE, out propertyValues[i]);
         else
         {
            foreach (string config in configs)
            {
               string[] configValues = new string[propertyNames.Length];
               for (int i = 0; i < propertyNames.Length; i++)
                  buildPropStorage.GetPropertyValue(propertyNames[i], config, (uint)_PersistStorageType.PST_PROJECT_FILE, out configValues[i]);
               if (!initialized)
               {
                  Array.Copy(configValues, propertyValues, propertyNames.Length);
                  initialized = true;
               }
               else
               {
                  bool equals = true;
                  for (int i = 0; i < propertyNames.Length; i++)
                     if (!(equals = (propertyValues[i] != configValues[i])))
                        break;

                  if (!equals)
                  {
                     // multiple config with different value for the property
                     for (int i = 0; i < propertyNames.Length; i++)
                        propertyValues[i] = string.Empty;
                     break;
                  }
               }
            }
         }

         return string.Join(",", propertyValues);
      }

      public void Dispose()
      { }

      #endregion
   }
}
