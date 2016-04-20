using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuBuild.VS
{
   public class EnumDescription
   {
      public int Key { get; }
      public string Value { get; }
      public string Description { get; }

      public EnumDescription(int key, string value, string description)
      {
         this.Key = key;
         this.Value = value;
         this.Description = description;
      }

      public override string ToString() => Value;
   }
}
