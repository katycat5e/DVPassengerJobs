using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PassengerJobsMod
{
    static class GameObjectDumper
    {
        public static string DumpObject( GameObject obj )
        {
            if( obj == null ) return "";

            var sb = new StringBuilder();
            DumpObjRecursive(obj, sb, "");
            return sb.ToString();
        }

        private static void DumpObjRecursive( GameObject obj, StringBuilder sb, string indent )
        {
            sb.AppendFormat("\n{0}[Object: {1}]", indent, obj.name);

            string subIndent = indent + '\t';
            foreach( Transform t in obj.transform )
            {
                DumpObjRecursive(t.gameObject, sb, subIndent);
            }

            foreach( Component c in obj.GetComponents<Component>() )
            {
                if( c ) sb.AppendFormat("\n{0}[Component: {1}]", subIndent, c.GetType().Name);
            }
        }
    }
}
