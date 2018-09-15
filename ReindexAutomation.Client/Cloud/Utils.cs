using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wintellect.PowerCollections;

namespace ReindexAutomation.Client.Cloud
{
    //TODO: LinkedHashMap
    public static class Utils
    {
        public static IDictionary<string, object> MakeMap(params object[] keyVals)
        {
            if ((keyVals.Length & 0x01) != 0)
            {
                throw new ArgumentException("Arguments should be key,value");
            }
            var propMap = new OrderedDictionary<string, object>();
            for (var i = 0; i < keyVals.Length; i += 2)
            {
                propMap.Add(keyVals[i].ToString(), keyVals[i + 1]);
            }
            return propMap;
        }
    }
}
