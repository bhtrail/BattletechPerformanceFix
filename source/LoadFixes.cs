using Harmony;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static BattletechPerformanceFix.Extensions;

namespace BattletechPerformanceFix;

public class LoadFixes : Feature
{
    public void Activate()
    {
        if (AppDomain.CurrentDomain.GetAssemblies()
            .Any(asm => asm.GetName().Name == "Turbine"))
        {
            Log.Main.Info?.Log("LoadFixes disabled (Turbine is installed, and faster than LoadFixes)");
        }
        else
        {
            TrapAndTerminate("Patch HBS.Util.JSONSerializationUtility.StripHBSCommentsFromJSON", () =>
            {
                var strip = Main.CheckPatch( AccessTools.Method(typeof(HBS.Util.JSONSerializationUtility), "StripHBSCommentsFromJSON")
                    , "29006a2218c101f065bd70c30d7147495d0101799a09fe72e4d969f92a1d90fd");
                Main.harmony.Patch( strip
                    , new(typeof(DontStripComments).GetMethod(nameof(DontStripComments.Prefix)))
                    , null);
            });
        }
    }
}

public class DontStripComments {
    // Copied from HBS.Utils.JSONSerializationUtility temporarily
    public static string HBSStripCommentsMirror(string json)
    {
        return TrapAndTerminate("HBSStripCommentsMirror", () =>
        {
            var self = new Traverse(typeof(HBS.Util.JSONSerializationUtility));
            var csp = self.Field("commentSurroundPairs").GetValue<Dictionary<string,string>>();

            var str = string.Empty;
            var format = "{0}(.*?)\\{1}";
            foreach (var keyValuePair in csp)
            {
                str = str + string.Format(format, keyValuePair.Key, keyValuePair.Value) + "|";
            }
            var str2 = "\"((\\\\[^\\n]|[^\"\\n])*)\"|";
            var str3 = "@(\"[^\"]*\")+";
            var pattern = str + str2 + str3;
            return Regex.Replace(json, pattern, delegate (Match me)
            {
                foreach (var keyValuePair2 in csp)
                {
                    if (me.Value.StartsWith(keyValuePair2.Key) || me.Value.EndsWith(keyValuePair2.Value))
                    {
                        return string.Empty;
                    }
                }
                return me.Value;
            }, RegexOptions.Singleline);
        });
    }

    public static string StripComments(string json)
    {
            
        // Try to parse the json, if it doesn't work, use HBS comment stripping code.
        try
        {
            fastJSON.JSON.Parse(json);
            return json;
        }
        catch
        {
            return HBSStripCommentsMirror(json);
        }
    }

    public static bool Prefix(string json, ref string __result) {
        var res =  TrapAndTerminate("DontStripComments.Prefix", () =>
        {
            var sc = StripComments(json);
            if (sc == null)
                throw new("StripComments result is null");
            return sc;
        });
        __result = res;
        return false;
    }
}